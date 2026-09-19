using UmbConstants = Umbraco.Cms.Core.Constants;

namespace Our.Umbraco.TrueCopy.Rewriting;

/// <summary>
///     <c>Umbraco.ContentPicker</c> - a single <c>umb://document/&lt;guid&gt;</c>.
/// </summary>
public sealed class ContentPickerRewriter : ILinkRewriter
{
    public bool CanRewrite(string propertyEditorAlias)
        => propertyEditorAlias == UmbConstants.PropertyEditors.Aliases.ContentPicker;

    public string? Rewrite(string value, RewriteContext context)
    {
        if (!DocumentUdi.TryParse(value, out Guid? target))
        {
            return null;
        }

        Guid? copy = context.ResolveDocument(target.Value);
        return copy is null ? null : DocumentUdi.Format(copy.Value);
    }
}
