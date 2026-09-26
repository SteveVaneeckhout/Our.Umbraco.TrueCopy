using System.Text.Json.Nodes;
using UmbConstants = Umbraco.Cms.Core.Constants;

namespace Our.Umbraco.TrueCopy.Rewriting;

/// <summary>
///     <c>Umbraco.MultiUrlPicker</c> - a JSON array of link objects.
/// </summary>
/// <remarks>
///     Umbraco 17 persists <c>{ name, target, udi, url, queryString, culture }</c>; an internal link has
///     <c>udi</c>, an external one has <c>url</c> and is left alone. Older values can also carry
///     <c>type</c>/<c>unique</c>, which v18 no longer writes but still reads - working on the JSON tree
///     rather than a model means those survive a rewrite instead of being silently dropped.
/// </remarks>
public sealed class MultiUrlPickerRewriter : ILinkRewriter
{
    public bool CanRewrite(string propertyEditorAlias)
        => propertyEditorAlias == UmbConstants.PropertyEditors.Aliases.MultiUrlPicker;

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

            changed |= RewriteUdi(entry, context);
            changed |= RewriteLegacyUnique(entry, context);
        }

        return changed ? links.ToJsonString() : null;
    }

    private static bool RewriteUdi(JsonObject entry, RewriteContext context)
    {
        if (!DocumentUdi.TryParse(entry.GetString("udi"), out Guid? target))
        {
            return false;
        }

        Guid? copy = context.ResolveDocument(target.Value);
        if (copy is null)
        {
            return false;
        }

        entry["udi"] = DocumentUdi.Format(copy.Value);
        return true;
    }

    private static bool RewriteLegacyUnique(JsonObject entry, RewriteContext context)
    {
        if (!string.Equals(entry.GetString("type"), UmbConstants.UdiEntityType.Document,
                StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParse(entry.GetString("unique"), out Guid target))
        {
            return false;
        }

        Guid? copy = context.ResolveDocument(target);
        if (copy is null)
        {
            return false;
        }

        entry["unique"] = copy.Value.ToString();
        return true;
    }
}
