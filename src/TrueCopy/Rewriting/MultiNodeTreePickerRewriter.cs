using UmbConstants = Umbraco.Cms.Core.Constants;

namespace Our.Umbraco.TrueCopy.Rewriting;

/// <summary>
///     <c>Umbraco.MultiNodeTreePicker</c> - a comma-separated list of UDIs, which for a picker configured
///     over more than one tree can mix documents, media and members in one value.
/// </summary>
/// <remarks>
///     Entries that are not document UDIs are copied through verbatim, including whitespace and anything
///     unparseable, so a mixed picker survives intact. Only the document entries can have moved.
/// </remarks>
public sealed class MultiNodeTreePickerRewriter : ILinkRewriter
{
    public bool CanRewrite(string propertyEditorAlias)
        => propertyEditorAlias == UmbConstants.PropertyEditors.Aliases.MultiNodeTreePicker;

    public string? Rewrite(string value, RewriteContext context)
    {
        string[] entries = value.Split(',');
        var changed = false;

        for (var i = 0; i < entries.Length; i++)
        {
            if (!DocumentUdi.TryParse(entries[i], out Guid? target))
            {
                continue;
            }

            Guid? copy = context.ResolveDocument(target.Value);
            if (copy is null)
            {
                continue;
            }

            entries[i] = DocumentUdi.Format(copy.Value);
            changed = true;
        }

        return changed ? string.Join(",", entries) : null;
    }
}
