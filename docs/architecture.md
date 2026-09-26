# TrueCopy

> How this package works, and why. For getting it running locally see
> [development.md](development.md); for what it does for an editor see the
> [README](https://github.com/SteveVaneeckhout/Our.Umbraco.TrueCopy#readme). Paths below are relative to `src/TrueCopy/` unless stated otherwise.


Umbraco's built-in **Copy** duplicates a document and its descendants, but copies property values
verbatim. Every Content Picker, Document Picker, Multi URL Picker and rich-text internal link in the
copies therefore still points at the *original* pages. Copy a site section to build a new one and you
get a set of pages that all link back into the old section, to be fixed by hand, page by page.

TrueCopy adds a **True Copy…** item to a document's tree menu. It performs the copy Umbraco already
performs, then walks the copies and repoints every internal link whose target was itself part of the
copy.

Links pointing at pages *outside* the copy are left exactly as they are — they are still correct —
but they are counted and listed in the summary the editor sees afterwards, because "this copy still
links into the old tree" is the one thing they cannot see for themselves.

**The built-in Copy is untouched.** TrueCopy is a separate action; Umbraco's own Copy behaves exactly
as it did before, which `corecopy` in the verification below asserts on purpose.

## Layout

```
TrueCopy/
  Composers/    DI registration, the notification handler, the rewriter collection, the OpenAPI document
  Controllers/  the backoffice API (one endpoint)
  Services/     orchestration, the per-operation key map, the notification handler
  Rewriting/    one ILinkRewriter per editor family - where all the risk lives
  ViewModels/   request/response models
  Resources/    .resx strings for the ProblemDetails the API returns (see Translating)
  Client/       TypeScript + Vite source for the entity action and the result modal
    src/lang/   the UI dictionaries, one file per language
  wwwroot/      Vite output, served at /App_Plugins/TrueCopy (generated, not committed)
```

## How the original-to-copy map is built

`ContentService.Copy` raises one `ContentCopiedNotification` per copied node — the root and every
descendant — and the scoped publisher dispatches them **together, in a single call**, once the copy's
scope has completed:

```csharp
void Handle(IEnumerable<ContentCopiedNotification> notifications);
```

So by the time the copy call returns, `TrueCopyKeyMapHandler` has seen every `(Original, Copy)` pair.
That collection *is* the map. It works for every entry point — the backoffice, the management API, or
a direct `IContentService.Copy` call — because they all funnel into the same method.

**Why not the `relateDocumentOnCopy` relations.** Umbraco can record a relation per copied node, which
would give the same mapping. But it only does so when the editor ticked "relate to original", which
would make TrueCopy's correctness depend on an unrelated checkbox; and the relation table accumulates
every copy ever made, so identifying *this* copy's rows means guessing at a time window.

The handler is registered globally and therefore runs on every copy in the installation. Its first act
is to ask `TrueCopyOperationAccessor` whether a TrueCopy operation is running and return if not. That
one lookup is what keeps Umbraco's own Copy completely unaffected.

## The rewriters

```csharp
bool CanRewrite(string propertyEditorAlias);
string? Rewrite(string value, RewriteContext context);   // null == unchanged
```

**Returning `null` rather than the input is the safety property the package rests on.** A value we did
not need to change is never deserialised and re-serialised, so round-trip fidelity only has to hold
for values we genuinely rewrote — and TrueCopy never saves a document it had no business touching.

`LinkRewriterCollection` is an Umbraco `BuilderCollectionBase`, so a site can `Append` its own
rewriter for a custom editor.

Stored value shapes, each confirmed against real rows in this repo's database rather than assumed:

| Editor | Stored as |
| --- | --- |
| `Umbraco.ContentPicker` | `umb://document/<guid-n>` |
| `Umbraco.MultiNodeTreePicker` | comma-separated UDIs, types mixed: `umb://document/…,umb://media/…` |
| `Umbraco.MultiUrlPicker` | JSON array of `{name,target,unique,type,udi,url,queryString,culture}`; internal links carry `udi`, external ones `url` |
| `Umbraco.RichText` | `{"markup":"…","blocks":{contentData,settingsData,expose,layout}}` |
| `Umbraco.BlockList` / `BlockGrid` / `SingleBlock` | `{layout,contentData,settingsData,expose}` |

Note that v17 still *persists* `unique` and `type` on Multi URL Picker entries (as nulls), so both the
`udi` form and the older `type`+`unique` pairing are handled.

### Everything works on the JSON tree, not on Umbraco's models

`BlockPropertyValue.Value` is `object?`, so a nested block value round-trips through a typed model as
a `JsonElement` and has to be reconstructed by hand — and any field TrueCopy does not model would be
silently dropped on the way back out. Editing `JsonNode` in place changes only the GUIDs that matched
and leaves every other byte alone, which is the behaviour you want from something rewriting other
people's content.

### Rich text has three link forms, and a media trap

All three appear in real data and all three are handled, each rewritten back into the form it arrived
in:

- Umbraco 17: `<a href="/{localLink:<guid>}" type="document">`
- older: `{localLink:umb://document/<guid>}`
- pre-v7: `{localLink:1234}` — resolved through the id half of the map

The match is anchored on the `<a>` tag rather than the token alone so the tag's `type` attribute can be
read. A `{localLink:<guid>}` with `type="media"` is a media link: its GUID is never in the copy map, so
rewriting it is a no-op either way — but **reporting it as a document link left pointing outside the
copy would be a lie**, and that report is the feature.

Block placeholders in the markup (`<umb-rte-block data-content-key="…">`) reference block content
keys, not documents. Umbraco's copy re-keys those consistently across `layout`, `contentData` and
`expose`; rewriting one would detach a block from its own layout entry, so they are left alone.

### Blocks recurse by `editorAlias`

Umbraco 17 persists `editorAlias` alongside each nested value, so each one says which editor it is and
nothing has to be guessed. Where it is missing — values written by an older version — the block's
`contentTypeKey` is resolved through `IContentTypeService` instead, cached per operation. Guessing the
editor from the value's shape is how a rewriter starts corrupting data, so there is no third fallback:
an unidentifiable value is skipped.

### Why there is no reference pre-filter

`DataValueReferenceFactoryCollection.GetAllReferences(properties, propertyEditors)` looks like the
ideal cheap test for "does this document reference anything in the map". It is not usable here: it
throws `ArgumentException` when a block property value has no resolved `PropertyType`, which is exactly
the state a document loaded through `IContentService.GetById` is in. The rewriters are cheap enough
(most start with a substring test) that the filter would not have earned its risk anyway.

## API

`POST /umbraco/truecopy/api/v1/copy`, requiring Content section access. Browsable at
`/umbraco/swagger` under the `truecopy` document.

```jsonc
{ "sourceId": "<guid>", "targetParentId": "<guid|null>", "includeDescendants": true, "relateToOriginal": false }
```

Section access alone is not enough — copying is a content write — so the request is authorized against
the two specific documents, the same two permissions core's own `CopyDocumentController` checks:
`Umb.Document.Duplicate` on the source and `Umb.Document.Create` on the target.

**`ActionLetter`, not `ActionAlias`.** In Umbraco 17 (and 18) those two fields read backwards from their names:
`ActionCopy.ActionLetter` is `"Umb.Document.Duplicate"`, the permission a user actually holds, while
`ActionCopy.ActionAlias` is the legacy `"copy"`. Authorizing on the alias fails every check silently,
because nobody is ever granted a permission by that name.

**The OpenAPI document comes from Swashbuckle on Umbraco 17**, served at
`/umbraco/swagger/truecopy/swagger.json`. Operations are named HTTP method + action (`PostCopy`) so the
generated client's `postCopy` is the same on both branches. On Umbraco 18 a declared 401 or 403
`ProducesResponseType` breaks document generation; the controller declares neither, on either branch,
so it stays the same code.

## The client

There are no dashboards in this package — one `entityAction` and one `modal`.

The destination picker is **core's own duplicate modal** (`UMB_DUPLICATE_DOCUMENT_MODAL`, publicly
exported from `@umbraco-cms/backoffice/document`), so True Copy looks and behaves exactly like the
built-in Copy right up to the point where it fixes the links, and the allowed-parent filtering and tree
pre-expansion are core's code paths rather than a second implementation that could drift from them.

It needs no `UmbModalRouteRegistrationController`. The repo's routed-modal rule applies to
*collection*-based pickers — the user picker in ContentDashboard embeds `umb-collection`, whose router
fires the `navigationsuccess` that makes the modal manager force-close router-less modals. This modal
renders `umb-tree`, and core itself opens it with a plain `umbOpenModal`. Verified: it is still open
five seconds after opening.

```bash
cd Client
npm install            # NOT --legacy-peer-deps
npm run build          # or: npm run watch
npm run generate-client   # regenerate src/api from the live OpenAPI doc (site must be running)
```

Bump `version` in `Client/public/umbraco-package.json` on every client change — it is the `?umb__rnd=`
cache-buster. Static web assets are baked at build time, so after `npm run build` you must rebuild and
restart `Cms` before the new chunks are served. The running site locks `TrueCopy.dll`; stop it first.

## Translating

Text lives in two places, because it is displayed in two different ways.

**The UI** — `Client/src/lang/`. `en.ts` is the source of truth; every other file is a copy of it
with the values translated, registered by a `type: "localization"` manifest in `lang/manifest.ts`.
Adding a language is those two edits and nothing else. English (`en`) is Umbraco's default culture,
so it is loaded whatever the editor's language is and acts as the per-key fallback — never translate
`en.ts` in place, and never give it a regional culture like `en-us`, which would *not* be loaded
alongside another language and would break the fallback.

Two guards keep a translation honest, and both fail the build rather than the UI:

- Every non-English file is typed `Translation<TrueCopyLocalizations>`, so a missing key is a `tsc`
  error instead of a string that silently renders in English.
- `npm run build` runs `scripts/check-lang.mjs` first, which fails if an element or a manifest
  references a `trueCopy_*` key that `en.ts` does not define. `tsc` cannot catch that, because
  `localize.term()` takes a plain string.

Counts are function entries (`(count: number) => …`) rather than an `(s)` suffix, so a language with
different plural rules can express them. The number itself is rendered separately from the label, so
a translation supplies only the noun.

**The API's ProblemDetails** — `Resources/TrueCopyResources.resx`, plus one
`TrueCopyResources.<culture>.resx` per language, which .NET builds into a satellite assembly. These
cannot live in the client dictionary: they reach the editor through core's generic ProblemDetails
handling, so they are already prose by the time any of our TypeScript could see them. The controller
picks the culture from the calling user's `IUser.Language`, so an editor sees the failure in their
own language regardless of the server's. The `{0}` in `FailedDetail` is a raw
`ContentEditingOperationStatus` name and is deliberately left untranslated — it is the diagnostic
that makes a support report actionable.

## Tests

`dotnet test TrueCopy.Tests` covers the rewriters, which carry all the logic that is easy to get
quietly wrong. They are pure functions of (value, key map) — no Umbraco host, no container, no
database — for the same reason `AnomalyDetector` is a static class in ErrorDashboard.

Each rewriter is exercised for a target inside the map, a target outside it (untouched and reported),
a self-link, values it must ignore (media and member UDIs, external URLs), malformed input (returns
null, never throws), and the guard that an unmatched value returns `null` so nothing is written.

## Known limitations

- Links held in **media**, **members**, **dictionary items** or **templates** are out of scope.
- A custom property editor storing document references in a shape none of the rewriters understands is
  not rewritten — and is not reported either, because we cannot see it. Append an `ILinkRewriter` to
  cover it.
- The rewrite saves the copies, so they are drafts written by the copying user. TrueCopy never
  publishes anything.
- The notification handler is a site-wide registration. It returns on its first line unless a TrueCopy
  operation is running, but it is on the code path of every copy in the installation.
- Rewriting happens after the copy has been committed. If a rewrite fails, the copy still exists with
  its original links — the failure is logged per property and the copy is never left half-written, but
  it is not rolled back either.
