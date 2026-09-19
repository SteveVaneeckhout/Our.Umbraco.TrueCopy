using Our.Umbraco.TrueCopy.Rewriting;

namespace Our.Umbraco.TrueCopy.Tests;

/// <summary>
///     Builds the rewriter collection and a context over a given key map.
/// </summary>
/// <remarks>
///     No Umbraco host, no DI container, no database: every rewriter is a pure function of (value, map),
///     which is the whole reason the risky part of this package can be tested at all.
/// </remarks>
internal static class RewriteFixture
{
    /// <summary>An arbitrary original page and its copy.</summary>
    public static readonly Guid Original = new("11111111-1111-1111-1111-111111111111");

    public static readonly Guid Copy = new("22222222-2222-2222-2222-222222222222");

    /// <summary>A page that was not part of the copy, so links to it must be left alone.</summary>
    public static readonly Guid Outside = new("33333333-3333-3333-3333-333333333333");

    /// <summary>A second original/copy pair, for values holding more than one link.</summary>
    public static readonly Guid SecondOriginal = new("44444444-4444-4444-4444-444444444444");

    public static readonly Guid SecondCopy = new("55555555-5555-5555-5555-555555555555");

    public static LinkRewriterCollection Rewriters { get; } = new(() =>
    [
        new ContentPickerRewriter(),
        new MultiNodeTreePickerRewriter(),
        new MultiUrlPickerRewriter(),
        new RichTextRewriter(),
        new BlockEditorRewriter(),
    ]);

    /// <summary>A context mapping <see cref="Original" /> to <see cref="Copy" />, and the second pair.</summary>
    public static RewriteContext Context(
        IReadOnlyDictionary<int, int>? idMap = null,
        Func<Guid, string, string?>? nestedEditorAliasResolver = null)
    {
        var context = new RewriteContext(
            new Dictionary<Guid, Guid> { [Original] = Copy, [SecondOriginal] = SecondCopy },
            idMap,
            Rewriters,
            nestedEditorAliasResolver);

        context.BeginDocument(Copy, "A copy");
        context.BeginProperty("testProperty");
        return context;
    }

    /// <summary>The <c>umb://document/&lt;guid&gt;</c> form, as Umbraco stores it.</summary>
    public static string Udi(Guid key) => "umb://document/" + key.ToString("N");
}
