# Supporting your own property editor

> Writing an `ILinkRewriter` so TrueCopy understands a property editor it does not ship support for.
> For what TrueCopy does see the [README](https://github.com/SteveVaneeckhout/Our.Umbraco.TrueCopy#readme); for how it works inside, see
> [architecture.md](architecture.md).

TrueCopy ships rewriters for the five editor families Umbraco ships: Content Picker, Multinode
Treepicker, Multi URL Picker, Rich Text, and Block List / Block Grid / Single Block. Anything else —
a custom picker, a commercial package's editor, your own block-shaped payload — stores its document
references in a shape TrueCopy has never seen.

**An unrecognised editor is skipped silently, and cannot be reported.** TrueCopy leaves the value
untouched, which is safe, but it also cannot tell the editor that the copy still points at the
original, because it cannot see inside the value to know there is a reference there at all. That
second half is the real cost, and writing a rewriter is what buys it back.

## The contract

```csharp
public interface ILinkRewriter
{
    bool CanRewrite(string propertyEditorAlias);

    string? Rewrite(string value, RewriteContext context);
}
```

Two methods, and three rules.

**Return `null` when nothing changed.** Not the input — `null`. This is the safety property the whole
package rests on. A value TrueCopy did not need to change is never deserialised and re-serialised, so
round-trip fidelity only has to hold for values that genuinely were rewritten, and TrueCopy never
saves a document it had no business touching.

**Never throw.** A value you cannot parse is a value you leave alone. Malformed and half-migrated data
is normal in a real site, and a rewriter that throws on it would fail a copy for no good reason.
(TrueCopy does catch and log per property, but treat that as a backstop, not a design.)

**Work on the JSON tree, not a typed model.** Deserialising into your own model and re-serialising
silently drops any field you did not model — and property editors accumulate fields. Edit the parsed
tree in place and the parts you know nothing about survive untouched.

## The smallest possible rewriter

This is `ContentPickerRewriter` from the package itself, in full. A single `umb://document/<guid>`:

```csharp
using Our.Umbraco.TrueCopy.Rewriting;

public sealed class MyPickerRewriter : ILinkRewriter
{
    public bool CanRewrite(string propertyEditorAlias) => propertyEditorAlias == "Acme.DocumentPicker";

    public string? Rewrite(string value, RewriteContext context)
    {
        if (!DocumentUdi.TryParse(value, out Guid? target))
        {
            return null;   // not a document UDI: leave it alone
        }

        Guid? copy = context.ResolveDocument(target.Value);
        return copy is null ? null : DocumentUdi.Format(copy.Value);
    }
}
```

`DocumentUdi` is public, so you do not have to re-derive the format — including the detail that the
GUID is written in `"N"` form, without hyphens.

## Registering it

`LinkRewriterCollection` is an Umbraco ordered collection. Register from your own composer:

```csharp
public class MyComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<LinkRewriterCollectionBuilder>()
                  .Append<MyPickerRewriter>();
}
```

**The first rewriter that claims an alias handles it**, and nothing after it is consulted. So to
*replace* a built-in rather than add to them, insert ahead of it:

```csharp
builder.WithCollectionBuilder<LinkRewriterCollectionBuilder>()
       .InsertBefore<RichTextRewriter, MyRichTextRewriter>();
```

`Insert`, `InsertAfter`, `Remove` and `Replace` are all available too — they come from Umbraco's
`OrderedCollectionBuilderBase`, not from this package.

## Values holding more than one reference

Accumulate whether anything changed, and serialise only if something did. This is the idiom the
built-in `MultiUrlPickerRewriter` uses:

```csharp
public string? Rewrite(string value, RewriteContext context)
{
    if (JsonRewriting.TryParse(value) is not JsonArray links)
    {
        return null;
    }

    var changed = false;

    foreach (JsonNode? link in links)
    {
        if (link is not JsonObject entry)
        {
            continue;
        }

        if (DocumentUdi.TryParse(entry.GetString("udi"), out Guid? target))
        {
            Guid? copy = context.ResolveDocument(target.Value);
            if (copy is not null)
            {
                entry["udi"] = DocumentUdi.Format(copy.Value);
                changed = true;
            }
        }
    }

    return changed ? links.ToJsonString() : null;
}
```

`JsonRewriting.TryParse` returns `null` instead of throwing on malformed JSON, and
`JsonObject.GetString` returns `null` if the property is absent, null, or not a string. Both are
public for exactly this purpose.

Note that `ResolveDocument` is called for **every** reference found, including the ones that turn out
to be outside the copy. That is deliberate — see below.

## Block-shaped payloads

If your editor stores something Block-List-shaped — an object with `contentData` and/or
`settingsData` arrays, each entry carrying a `contentTypeKey` and a `values` array — you do not need
to walk it yourself:

```csharp
public string? Rewrite(string value, RewriteContext context)
{
    if (JsonRewriting.TryParse(value) is not JsonObject root)
    {
        return null;
    }

    return context.RewriteBlockStructure(root) ? root.ToJsonString() : null;
}
```

`RewriteBlockStructure` handles recursion into nested blocks, the `editorAlias` fallback for values
written by older Umbraco versions, and the inline-JSON round trip that a nested block value needs.
Each inner value is handed back to the whole rewriter collection, so your custom editors nested
inside blocks are rewritten too.

For a shape that is *not* block-like, recurse yourself with:

```csharp
string? rewritten = context.Rewriters.Rewrite(editorAlias, rawValue, context);
```

## The report is the point

`context.ResolveDocument(key)` returns the copy's key, or `null` when the target was not part of the
copy. The tally is a side effect of asking:

- a hit increments `RewrittenLinkCount`;
- a miss records an `ExternalReference` — the copy it was found on, the property alias, and what it
  still points at — deduplicated, and shown to the editor afterwards as *"still points at the
  original"*.

So **call `ResolveDocument` on every reference you find**, even when you expect it to miss. A rewriter
that only calls it when it intends to rewrite produces a silently incomplete report, and a rewriter
that finds references but rewrites none of them still earns its keep.

There is also `ResolveDocumentId(int)` for the pre-v7 integer form still sitting in old rich text. It
deliberately records nothing on a miss: there is no key for an unmapped id, and the report is keyed
on document keys.

## Testing it

Rewriters are pure functions of `(value, key map)`. `RewriteContext` has a public constructor, so a
test needs no Umbraco host, no DI container and no database:

```csharp
var original = new Guid("11111111-1111-1111-1111-111111111111");
var copy = new Guid("22222222-2222-2222-2222-222222222222");

var context = new RewriteContext(
    new Dictionary<Guid, Guid> { [original] = copy },
    rewriters: new LinkRewriterCollection(() => [new MyPickerRewriter(), new ContentPickerRewriter()]));

context.BeginDocument(copy, "A copy");
context.BeginProperty("myPicker");

string? result = new MyPickerRewriter().Rewrite(DocumentUdi.Format(original), context);

Assert.AreEqual(DocumentUdi.Format(copy), result);
Assert.AreEqual(1, context.RewrittenLinkCount);
Assert.IsEmpty(context.ExternalReferences);
```

One trap worth knowing: if your rewriter recurses — via `RewriteBlockStructure` or
`context.Rewriters` — **put the built-in rewriters in the test collection too**, as above. Recursion
hands each nested value back to the collection, so a collection containing only your own rewriter
will silently leave every nested `Umbraco.ContentPicker` alone, and your test will fail for a reason
that has nothing to do with your code. At runtime the collection comes from DI and already holds
them.

## What is out of scope

TrueCopy rewrites **document** references only. Media, members, dictionary items and templates are
not rewritten and not reported, because copying documents does not move any of them — the original
targets are still the correct targets.

For the same reason, `DocumentUdi.TryParse` rejects `umb://media/…` and `umb://member/…`. If your
editor mixes entity types in one value, filter on the type rather than assuming every UDI is a
document: reporting a media link as "still pointing at the original" would be plainly wrong.
