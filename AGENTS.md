# AGENTS.md

Notes for anyone - human or otherwise - working in this repository. Everything here was paid for
once already; none of it is guesswork.

## What this is

`Our.Umbraco.TrueCopy`: an Umbraco 18 backoffice package, a copy that also repoints internal links at the copies. It is published to NuGet and
listed on the Umbraco Marketplace, so the public surface and the README are part of the product.

```
src/Cms/            the Umbraco host. A development harness, never shipped (IsPackable=false).
src/TrueCopy/      the package
src/TrueCopy.Tests/ MSTest
scripts/            tooling outside the build
docs/               architecture, development, extending, upgrading
```

Inside `src/TrueCopy/`:

```
  Composers/    DI registration, the notification handler, the rewriter collection
  Controllers/  the backoffice API (one endpoint)
  Rewriting/    one ILinkRewriter per editor family - where all the risk lives
  Services/     orchestration, the per-operation key map, the notification handler
  ViewModels/   request/response models
  Resources/    .resx for the ProblemDetails the API returns (see Translating)
  Client/       TypeScript + Vite source for the backoffice UI
    src/lang/   the UI dictionaries, one file per language
  wwwroot/      Vite output, served at /App_Plugins/TrueCopy (generated, NOT committed)
```

## Running it

```bash
dotnet run --project src/Cms
```

Backoffice at **https://localhost:44366/umbraco**, admin `hello@example.com`, password in
`src/Cms/appsettings.json`. The configured application URL is
`https://testsite1.127.0.0.1.nip.io:44366/`, which is the host the single uSync domain binds to.
Everything secret in this repository is deliberately public - it is a throwaway local harness.

On first boot the site creates a SQLite database, installs unattended, and imports `src/Cms/uSync/v18`.

**A running site holds the package DLL open**, so stop it before `dotnet build`:
`Get-Process -Name Cms | Stop-Process -Force`.

## Build, test, format

```bash
dotnet build Our.Umbraco.TrueCopy.slnx -p:BuildClient=false
dotnet test --solution Our.Umbraco.TrueCopy.slnx
dotnet format Our.Umbraco.TrueCopy.slnx --verify-no-changes
```

- **Tests are MSTest on Microsoft.Testing.Platform.** The .NET 10 SDK refuses to run MTP projects
  through the legacy VSTest target, so `global.json` carries
  `"test": { "runner": "Microsoft.Testing.Platform" }`. It is **not** `dotnet.config`, which looks
  plausible and does nothing. In this mode a project is `dotnet test --project <path>`, not
  positional, and `dotnet test A.csproj B.csproj` is rejected outright.
- Porting assertions from xUnit: `Assert.AreEqual(expected, actual, x)` takes a **delta**, where
  xUnit's third argument was a *decimal-place count* - carrying a `10` across turns a tight assertion
  into "within ±10". `Assert.AreEqual` compares collections by **reference**; use
  `CollectionAssert.AreEqual`. `Assert.Contains`/`DoesNotContain` do keep xUnit's
  `(substring, value)` order, unlike `StringAssert.Contains(value, substring)`.
- `dotnet format --verify-no-changes` is a CI gate. `.gitattributes` normalises the working tree to
  LF; without that it fails on line endings alone.
- `-p:BuildClient=false` skips the MSBuild target that shells out to npm. **That target only fires
  when the bundle is missing**, so it will happily reuse a stale `wwwroot` - which is exactly how
  source maps once shipped inside a release package. CI builds the client explicitly, then passes
  this flag everywhere.

## Dependencies

This package depends on nothing that is not published by **Microsoft or Umbraco**, and that is a
deliberate constraint, not an accident. Before adding a package, check whether the .NET SDK or
Umbraco already covers it. SourceLink, for instance, needs no PackageReference - it is in the SDK.

## Namespaces

The root namespace is `Our.Umbraco.TrueCopy`, which **shadows the global `Umbraco` namespace**. Inside
it, a fully-qualified `Umbraco.Cms.Core.Constants` resolves `Umbraco` to `Our.Umbraco` and fails
with `CS0246: The type or namespace name 'Cms' does not exist in the namespace 'Our.Umbraco'`. The
same applies to XML `cref` attributes, which fail as `CS1574`.

Use a file-scoped alias - `using UmbConstants = Umbraco.Cms.Core.Constants;` - which sits outside the
namespace and resolves globally. These references are usually fully qualified in the first place
because each package declares its own `Constants` class that shadows Umbraco's, so the alias fixes
both problems at once. For crefs, import the namespace and use the simple type name.

Deliberately **not** renamed, and not to be renamed: `App_Plugins/TrueCopy`, the bundle aliases,
`umbraco-package.json`'s `id`, and `Constants.ApiName` (`"truecopy"`, which is both the OpenAPI
document name and the route prefix `/umbraco/truecopy/api/v1/…`).

## Client workflow

```bash
cd src/TrueCopy/Client
npm install            # NOT --legacy-peer-deps
npm run build          # or: npm run watch
npm run generate-client   # regenerate src/api from the live OpenAPI doc (site must be running)
```

- **`npm install`, never `--legacy-peer-deps`.** Umbraco's docs suggest that flag, but it skips the
  peers (`lit`, `@umbraco-ui/uui`, `rxjs`) the TypeScript build needs for types. Without them you
  get a wall of "module has no exported member" errors.
- **Bump `version` in `Client/public/umbraco-package.json` on every client change.** Umbraco uses it
  as the cache-buster (`?umb__rnd=`); leave it and the browser serves the old bundle.
- **Static web assets are baked at build time.** After `npm run build`, new chunks are only served
  once the site is rebuilt and restarted.

Import rules: templating from `@umbraco-cms/backoffice/external/lit` (`@umbraco-cms/backoffice/lit`
does not exist; bare `lit` bundles a second copy and breaks reactive-element identity), elements
extend `UmbLitElement` from `@umbraco-cms/backoffice/lit-element`, and
`rollupOptions.external: [/^@umbraco/]` must stay - the backoffice resolves those specifiers through
the import map it serves at `/umbraco/backoffice/umbraco-package.json`.

## Localization

**No user-facing string belongs in a template.** Text comes from `Client/src/lang/en.ts` via
`this.localize.term(...)`, manifest labels are `"#area_key"`, and dates and numbers go through
`this.localize.date/number/relativeTime` - plain `Intl` follows the *browser* language, not the
backoffice one.

Two guards fail the build rather than the UI: `npm run build` runs `scripts/check-lang.mjs`, which
rejects a key `en.ts` does not define, and every non-English file is typed
`Translation<TrueCopyLocalizations>` so a missing key is a `tsc` error. Never translate `en.ts` in
place, and never give it a regional culture like `en-us` - `en` is Umbraco's default and acts as the
per-key fallback.

The `.resx` files in `src/TrueCopy/Resources` cover the one surface a browser cannot localize.

## OpenAPI

**`[ProducesResponseType(401)]` - and `(403)` - break the OpenAPI document.** Umbraco's
`BackOfficeSecurityRequirementsTransformer` adds **both** to every operation, so declaring either
yourself throws "An item with the same key has already been added. Key: 401" when the document is
generated. It surfaces as a 500 on `/umbraco/openapi/truecopy.json`, with nothing in the stack trace
pointing at your controller.

## What matters in this package

Read `docs/architecture.md` before touching the rewriters - they encode the stored shape of five
property editors, verified against real database rows. `docs/extending.md` is the public contract
for third parties and must stay true.

- **Returning `null` rather than the input is the safety property the package rests on.** A value
  that did not need changing is never deserialised and re-serialised, so round-trip fidelity only has
  to hold for values genuinely rewritten - and TrueCopy never saves a document it had no business
  touching.
- **Rewriters work on the JSON tree, never on typed models.** `BlockPropertyValue.Value` is
  `object?`, so typed round-tripping silently drops unmodelled fields.
- **Rich text has three link forms and a media trap.** v18 `<a href="/{localLink:<guid>}" type="document">`,
  the older `{localLink:umb://document/<guid>}`, and the pre-v7 `{localLink:1234}`. The match is
  anchored on the `<a>` tag so `type` can be read: a `type="media"` link must never be reported as a
  document link left pointing outside the copy, because that report is the feature.
  `<umb-rte-block data-content-key>` values are block keys, not documents - leave them alone.
- **Blocks recurse by `editorAlias`**, falling back to resolving `contentTypeKey` via
  `IContentTypeService`. There is deliberately no third fallback: guessing the editor from the value's
  shape is how a rewriter corrupts data.
- **`DataValueReferenceFactoryCollection.GetAllReferences` throws on block values.** It looks like the
  way to ask "what does this document reference", but it needs every `BlockPropertyValue` to have a
  resolved `PropertyType`, which a document from `IContentService.GetById` does not have. That is why
  there is no reference pre-filter.
- **`IAction.ActionLetter` holds the permission, `ActionAlias` holds the legacy name** - the two read
  backwards from their names in Umbraco 18. `ActionCopy.ActionLetter` is `"Umb.Document.Duplicate"`,
  which is what a user is actually granted. Authorizing a `ContentPermissionResource` on the alias
  fails every check silently.
- **The Multi URL Picker's editor value uses UDI entity types, not `LinkType` names.** Posting
  `type: "Content"` through the management API is accepted and stores a link with a null `udi`;
  `type: "document"` is what the backoffice sends and what produces a usable link.
- The copy modal is core's `UMB_DUPLICATE_DOCUMENT_MODAL`, opened with a plain `umbOpenModal`. That
  is fine: the routed-modal rule applies to `umb-collection` pickers, not `umb-tree` ones.

## The fixture

**The fixture is deliberate and is the only way to re-verify the rewriters.** The *Theme Park*
document type carries four `tc*` properties plus the *TrueCopy Test Block* element type and two data
types; none are used by templates. **Alton Towers** and **Thorpe Park** hold links to each other
(inside *United Kingdom*), a link to **Europa-Park** (deliberately outside it, so the "still points at
the original" report is non-empty), a self-link nested in a block, an external URL and a rich-text
`{localLink:…}`. To exercise everything: True Copy *United Kingdom* into *Europe* **with Include
descendants on** - without it you copy one page and prove almost nothing.

`src/Cms/uSync/v18` was re-exported from scratch, so it contains no delete tombstones and matches the
database exactly. A plain uSync Export **adds and updates files but never removes stale ones**, which
is how a domain pointing at a long-deleted node survived in the original export - empty the folder
first if you want a clean one.

## Verifying a change

The Chrome extension is not installed here. Use the Playwright browsers from another project:

```bash
PLAYWRIGHT_BROWSERS_PATH="C:/Users/zippy/AppData/Local/ms-playwright" node script.mjs
```

Install `playwright` (the library only) into a scratch directory with
`PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD=1`. Playwright's CSS engine **pierces open shadow roots**, so
ordinary selectors work against the backoffice. Log in at `#username-input` (type `text`, not
`email`) and `#password-input`, submit `#umb-login-button`, and wait for the form explicitly -
`isVisible()` does not wait and returns false before the page has rendered.

For server-side checks, get a token with the `.env` client credentials against
`POST /umbraco/management/api/v1/security/back-office/token` (`grant_type=client_credentials`).
**A fresh clone has no API user** - uSync does not export users, so create one in the backoffice
first.

For database assertions the site runs on SQLite and there is no `sqlite3` CLI on this machine. Use a
file-based C# script - `dotnet run q.cs` with `#:package Microsoft.Data.Sqlite@10.0.10` on the first
line - and **stop the site first**, because it holds the file and a WAL. Note that file-based apps
disable reflection-based JSON by default; add
`#:property JsonSerializerIsReflectionEnabledByDefault=true` if you need it. Do not put such a script
under `src/Cms/`: the SDK globs it into the project and the build fails with
`CS9298: '#:' directives can be only used in file-based programs`.

**The Bash tool mangles backslashes and can inject control characters into heredocs.** `\\`
collapses to `\` even inside a quoted heredoc. Anything with awkward escaping should go through a
file write instead. Use forward slashes for Windows paths - .NET accepts them.

## Releasing

Version lives in `Directory.Build.props`. A published GitHub Release whose tag is the version
(`v1.0.0`, or `v1.0.0-rc.1` marked pre-release) triggers `.github/workflows/release.yml`, which
packs with `-p:Version=` from the tag and pushes to NuGet using **trusted publishing** - an OIDC
exchange, no API key. The nuget.org policy is keyed on the workflow **file name**, so `release.yml`
must not be renamed. Bump `Client/public/umbraco-package.json` too, and add to `CHANGELOG.md`.
