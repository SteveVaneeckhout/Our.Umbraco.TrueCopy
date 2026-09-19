using System.Text.Json.Nodes;
using UmbConstants = Umbraco.Cms.Core.Constants;

namespace Our.Umbraco.TrueCopy.Rewriting;

/// <summary>
///     <c>Umbraco.BlockList</c>, <c>Umbraco.BlockGrid</c> and <c>Umbraco.SingleBlock</c> - and, via
///     <see cref="RewriteBlocks" />, the <c>blocks</c> half of a rich text value.
/// </summary>
/// <remarks>
///     Blocks are where most real links live, and a top-level scan cannot see them: a picker inside a
///     block is a property value nested inside <c>contentData[].values[].value</c>. This rewriter walks
///     that structure and hands each nested value back to whichever rewriter owns its editor, so the
///     nesting can go as deep as the content model does.
///     <para>
///         <c>layout</c>, <c>expose</c>, and each block's <c>key</c>/<c>contentTypeKey</c> are never
///         touched. Those keys identify blocks, not documents; <c>ContentService.Copy</c> deep-clones them
///         unchanged, and the layout refers to them by value, so rewriting one would detach a block from
///         its own layout entry.
///     </para>
/// </remarks>
public sealed class BlockEditorRewriter : ILinkRewriter
{
    private static readonly string[] SupportedAliases =
    [
        UmbConstants.PropertyEditors.Aliases.BlockList,
        UmbConstants.PropertyEditors.Aliases.BlockGrid,
        "Umbraco.SingleBlock",
    ];

    public bool CanRewrite(string propertyEditorAlias) => SupportedAliases.Contains(propertyEditorAlias);

    public string? Rewrite(string value, RewriteContext context)
    {
        if (JsonRewriting.TryParse(value) is not JsonObject root)
        {
            return null;
        }

        return RewriteBlocks(root, context) ? root.ToJsonString() : null;
    }

    /// <summary>
    ///     Rewrites every nested property value in a block structure, in place.
    /// </summary>
    /// <returns>Whether anything changed.</returns>
    internal static bool RewriteBlocks(JsonObject blockValue, RewriteContext context)
    {
        var changed = false;

        foreach (var collectionName in new[] { "contentData", "settingsData" })
        {
            if (blockValue[collectionName] is not JsonArray items)
            {
                continue;
            }

            foreach (JsonNode? item in items)
            {
                if (item is JsonObject block)
                {
                    changed |= RewriteBlock(block, context);
                }
            }
        }

        return changed;
    }

    private static bool RewriteBlock(JsonObject block, RewriteContext context)
    {
        if (block["values"] is not JsonArray values)
        {
            return false;
        }

        var changed = false;

        foreach (JsonNode? entry in values)
        {
            if (entry is JsonObject property)
            {
                changed |= RewriteBlockProperty(block, property, context);
            }
        }

        return changed;
    }

    private static bool RewriteBlockProperty(JsonObject block, JsonObject property, RewriteContext context)
    {
        string? editorAlias = ResolveEditorAlias(block, property, context);
        if (string.IsNullOrEmpty(editorAlias) || property["value"] is not { } valueNode)
        {
            return false;
        }

        // A nested value is stored either as a plain string (pickers, rich text markup) or as inline JSON
        // (a block editor nested in a block editor). Rewriters always speak strings, so unwrap on the way
        // in and rewrap in the same shape on the way out - writing a JSON object back as a string, or the
        // reverse, would be a data change Umbraco could not read.
        var isJsonValue = valueNode is JsonValue value && value.TryGetValue(out string? _);
        string raw = isJsonValue ? valueNode.GetValue<string>() : valueNode.ToJsonString();

        string? rewritten = context.Rewriters.Rewrite(editorAlias, raw, context);
        if (rewritten is null)
        {
            return false;
        }

        property["value"] = isJsonValue ? JsonValue.Create(rewritten) : JsonRewriting.TryParse(rewritten);
        return true;
    }

    /// <summary>
    ///     Umbraco 18 persists <c>editorAlias</c> alongside each nested value, so normally it says which
    ///     editor it is. Where it is missing - values written by an older version - fall back to resolving
    ///     the block's content type, which is what <c>TrueCopyService</c> supplies the resolver for.
    /// </summary>
    private static string? ResolveEditorAlias(JsonObject block, JsonObject property, RewriteContext context)
    {
        if (property.GetString("editorAlias") is { Length: > 0 } alias)
        {
            return alias;
        }

        if (context.NestedEditorAliasResolver is null ||
            property.GetString("alias") is not { Length: > 0 } propertyAlias ||
            !Guid.TryParse(block.GetString("contentTypeKey"), out Guid contentTypeKey))
        {
            return null;
        }

        return context.NestedEditorAliasResolver(contentTypeKey, propertyAlias);
    }
}
