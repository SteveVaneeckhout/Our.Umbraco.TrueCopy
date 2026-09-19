using System.Text.Json.Nodes;

namespace Our.Umbraco.TrueCopy.Rewriting;

/// <summary>
///     A document reference that was found in a copied page but pointed at something outside the copy,
///     and was therefore deliberately left alone.
/// </summary>
/// <param name="DocumentKey">The copy the reference was found on.</param>
/// <param name="DocumentName">That copy's name, for the report.</param>
/// <param name="PropertyAlias">The property the reference sits in.</param>
/// <param name="TargetKey">What it points at. Still the original target, which is correct.</param>
public record ExternalReference(Guid DocumentKey, string? DocumentName, string PropertyAlias, Guid TargetKey);

/// <summary>
///     Everything a rewriter needs for one TrueCopy operation: the original-to-copy key map, a place to
///     record what it did, and a way back into the rewriter collection so block editors can recurse.
/// </summary>
/// <remarks>
///     Rewriters never touch a service. They ask <see cref="ResolveDocument" /> whether a target was part
///     of the copy and get back the copy's key or nothing; the tally is a side effect of asking. That is
///     what keeps every rewriter a pure function of (value, map) and therefore testable without an
///     Umbraco host - the same bargain <c>AnomalyDetector</c> makes in ErrorDashboard.
/// </remarks>
public sealed class RewriteContext
{
    private readonly IReadOnlyDictionary<Guid, Guid> _keyMap;
    private readonly IReadOnlyDictionary<int, int> _idMap;
    private readonly List<ExternalReference> _externalReferences = [];
    private readonly HashSet<(Guid, string, Guid)> _seenExternal = [];

    private Guid _currentDocumentKey;
    private string? _currentDocumentName;
    private string _currentPropertyAlias = string.Empty;

    public RewriteContext(
        IReadOnlyDictionary<Guid, Guid> keyMap,
        IReadOnlyDictionary<int, int>? idMap = null,
        LinkRewriterCollection? rewriters = null,
        Func<Guid, string, string?>? nestedEditorAliasResolver = null)
    {
        _keyMap = keyMap;
        _idMap = idMap ?? new Dictionary<int, int>();
        Rewriters = rewriters ?? LinkRewriterCollection.Empty;
        NestedEditorAliasResolver = nestedEditorAliasResolver;
    }

    /// <summary>
    ///     The full set of rewriters, so <see cref="BlockEditorRewriter" /> can hand each nested property
    ///     value back to whichever rewriter owns its editor.
    /// </summary>
    /// <remarks>
    ///     Reached through the context rather than injected into the block rewriter because the block
    ///     rewriter is itself a member of the collection - constructor injection would be a cycle.
    /// </remarks>
    public LinkRewriterCollection Rewriters { get; }

    /// <summary>
    ///     Last resort for a nested block property whose persisted JSON carries no <c>editorAlias</c>:
    ///     given the block's content type key and the property alias, say which editor it is.
    /// </summary>
    /// <remarks>
    ///     Supplied by <c>TrueCopyService</c>, which has <c>IContentTypeService</c>. Null in unit tests,
    ///     where every fixture writes the alias the way Umbraco 18 does.
    /// </remarks>
    public Func<Guid, string, string?>? NestedEditorAliasResolver { get; }

    /// <summary>How many individual links were repointed at a copy.</summary>
    public int RewrittenLinkCount { get; private set; }

    /// <summary>References left pointing outside the copy, deduplicated per document/property/target.</summary>
    public IReadOnlyList<ExternalReference> ExternalReferences => _externalReferences;

    /// <summary>Names the document whose values are being rewritten, for the report.</summary>
    public void BeginDocument(Guid documentKey, string? documentName)
    {
        _currentDocumentKey = documentKey;
        _currentDocumentName = documentName;
    }

    /// <summary>Names the property being rewritten, for the report.</summary>
    public void BeginProperty(string propertyAlias) => _currentPropertyAlias = propertyAlias;

    /// <summary>
    ///     Maps a referenced document key onto its copy.
    /// </summary>
    /// <returns>
    ///     The copy's key when the target was part of this copy; <c>null</c> when it was not - in which
    ///     case the existing reference is already correct and is recorded as an external reference.
    /// </returns>
    /// <remarks>
    ///     The root of the copy is in the map like every other node, so a page that links to itself is
    ///     repointed at its own copy with no special case. That is the whole job when a single page is
    ///     copied on its own, and the reason doing so is not a no-op.
    /// </remarks>
    public Guid? ResolveDocument(Guid targetKey)
    {
        if (_keyMap.TryGetValue(targetKey, out Guid copyKey))
        {
            RewrittenLinkCount++;
            return copyKey;
        }

        RecordExternal(targetKey);
        return null;
    }

    /// <summary>
    ///     Maps a referenced document's integer id onto its copy's id, for the pre-v7 <c>{localLink:1234}</c>
    ///     form that can still be sitting in old rich text.
    /// </summary>
    public int? ResolveDocumentId(int targetId)
    {
        if (_idMap.TryGetValue(targetId, out int copyId))
        {
            RewrittenLinkCount++;
            return copyId;
        }

        // No external reference recorded: we have no key for an unmapped id, and the report is keyed on
        // document keys. An unmapped legacy link is by definition pointing outside the copy already.
        return null;
    }

    /// <summary>
    ///     Rewrites every nested property value inside a block-shaped payload, in place, and reports
    ///     whether anything changed.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         For a custom property editor that stores a Block List-shaped value: an object carrying
    ///         <c>contentData</c> and/or <c>settingsData</c> arrays whose entries each hold a
    ///         <c>contentTypeKey</c> and a <c>values</c> array. Recursion into nested blocks, the
    ///         <c>editorAlias</c> fallback and the inline-JSON round trip are all handled here, so a
    ///         third-party rewriter does not have to reimplement any of it.
    ///     </para>
    ///     <para>
    ///         Obeys the same contract as <see cref="ILinkRewriter.Rewrite" />: when this returns
    ///         <c>false</c>, return <c>null</c> rather than re-serialising a value nothing changed in.
    ///     </para>
    /// </remarks>
    /// <param name="blockValue">The parsed block structure. Modified in place.</param>
    /// <returns><c>true</c> if at least one nested reference was repointed.</returns>
    public bool RewriteBlockStructure(JsonObject blockValue)
        => BlockEditorRewriter.RewriteBlocks(blockValue, this);

    private void RecordExternal(Guid targetKey)
    {
        if (_seenExternal.Add((_currentDocumentKey, _currentPropertyAlias, targetKey)))
        {
            _externalReferences.Add(
                new ExternalReference(_currentDocumentKey, _currentDocumentName, _currentPropertyAlias, targetKey));
        }
    }
}
