namespace Our.Umbraco.TrueCopy.Rewriting;

/// <summary>
///     Rewrites the document references held in one family of property editors' stored values.
/// </summary>
/// <remarks>
///     Umbraco has no write-side equivalent of <c>IDataValueReference.GetReferences</c> - it can tell you
///     what a value points at, but nothing in the CMS can repoint it - so each editor's stored shape has
///     to be understood individually. A site can append its own implementation to
///     <see cref="LinkRewriterCollection" /> to cover a custom editor.
/// </remarks>
public interface ILinkRewriter
{
    /// <summary>Whether this rewriter understands the stored shape of the given property editor.</summary>
    bool CanRewrite(string propertyEditorAlias);

    /// <summary>
    ///     Returns the rewritten value, or <c>null</c> if nothing in it needed changing.
    /// </summary>
    /// <remarks>
    ///     Returning <c>null</c> rather than the input is the safety property this whole package rests on:
    ///     a value we did not need to change is never deserialised and re-serialised, so round-trip
    ///     fidelity only has to hold for values we genuinely rewrote. It is also what stops TrueCopy
    ///     saving documents it had no business touching.
    ///     Implementations must never throw on malformed input - a value that cannot be parsed is a value
    ///     we leave alone.
    /// </remarks>
    string? Rewrite(string value, RewriteContext context);
}
