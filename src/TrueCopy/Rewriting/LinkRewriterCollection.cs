using Umbraco.Cms.Core.Composing;

namespace Our.Umbraco.TrueCopy.Rewriting;

/// <summary>
///     The ordered set of <see cref="ILinkRewriter" />s. First one that claims the editor alias wins.
/// </summary>
public class LinkRewriterCollection : BuilderCollectionBase<ILinkRewriter>
{
    public LinkRewriterCollection(Func<IEnumerable<ILinkRewriter>> items)
        : base(items)
    {
    }

    /// <summary>An empty collection, for contexts built without one (unit tests of a single rewriter).</summary>
    public static LinkRewriterCollection Empty { get; } = new(() => []);

    /// <summary>
    ///     Hands a value to whichever rewriter owns its editor.
    /// </summary>
    /// <returns>The rewritten value, or <c>null</c> if no rewriter claimed it or nothing changed.</returns>
    public string? Rewrite(string propertyEditorAlias, string value, RewriteContext context)
    {
        if (string.IsNullOrWhiteSpace(propertyEditorAlias) || string.IsNullOrEmpty(value))
        {
            return null;
        }

        foreach (ILinkRewriter rewriter in this)
        {
            if (rewriter.CanRewrite(propertyEditorAlias))
            {
                return rewriter.Rewrite(value, context);
            }
        }

        return null;
    }
}

public class LinkRewriterCollectionBuilder
    : OrderedCollectionBuilderBase<LinkRewriterCollectionBuilder, LinkRewriterCollection, ILinkRewriter>
{
    protected override LinkRewriterCollectionBuilder This => this;
}
