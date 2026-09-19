using Our.Umbraco.TrueCopy.Rewriting;
using Umbraco.Cms.Core;
using UmbConstants = Umbraco.Cms.Core.Constants;

namespace Our.Umbraco.TrueCopy.Tests;

[TestClass]
public class DocumentUdiTests
{
    [TestMethod]
    public void Format_matches_what_Umbraco_writes()
    {
        // The rewriters hand-roll the UDI string so they can run without the static UDI type registry
        // that UdiParser needs. This is the test that keeps the hand-rolled form honest.
        var key = Guid.NewGuid();

        Assert.AreEqual(new GuidUdi(UmbConstants.UdiEntityType.Document, key).ToString(), DocumentUdi.Format(key));
    }

    [TestMethod]
    [DataRow("umb://media/11111111111111111111111111111111")]
    [DataRow("umb://member/11111111111111111111111111111111")]
    [DataRow("umb://document/not-a-guid")]
    [DataRow("not a udi at all")]
    [DataRow("")]
    public void TryParse_rejects_anything_that_is_not_a_document_udi(string value)
        => Assert.IsFalse(DocumentUdi.TryParse(value, out _));
}

[TestClass]
public class ContentPickerRewriterTests
{
    private readonly ContentPickerRewriter _rewriter = new();

    [TestMethod]
    public void Repoints_a_link_to_a_copied_page()
    {
        RewriteContext context = RewriteFixture.Context();

        string? result = _rewriter.Rewrite(RewriteFixture.Udi(RewriteFixture.Original), context);

        Assert.AreEqual(RewriteFixture.Udi(RewriteFixture.Copy), result);
        Assert.AreEqual(1, context.RewrittenLinkCount);
        Assert.IsEmpty(context.ExternalReferences);
    }

    [TestMethod]
    public void Leaves_a_link_outside_the_copy_alone_and_reports_it()
    {
        RewriteContext context = RewriteFixture.Context();

        string? result = _rewriter.Rewrite(RewriteFixture.Udi(RewriteFixture.Outside), context);

        Assert.IsNull(result);
        Assert.AreEqual(0, context.RewrittenLinkCount);
        ExternalReference reference = Assert.ContainsSingle(context.ExternalReferences);
        Assert.AreEqual(RewriteFixture.Outside, reference.TargetKey);
        Assert.AreEqual("testProperty", reference.PropertyAlias);
        Assert.AreEqual(RewriteFixture.Copy, reference.DocumentKey);
    }

    [TestMethod]
    public void A_page_that_links_to_itself_is_repointed_at_its_own_copy()
    {
        // The root of a copy is in the map like every other node, so a self-reference needs no special
        // case - and this is the entire job when a single page is copied on its own.
        var context = new RewriteContext(
            new Dictionary<Guid, Guid> { [RewriteFixture.Original] = RewriteFixture.Copy },
            rewriters: RewriteFixture.Rewriters);
        context.BeginDocument(RewriteFixture.Copy, "The copy");
        context.BeginProperty("self");

        string? result = _rewriter.Rewrite(RewriteFixture.Udi(RewriteFixture.Original), context);

        Assert.AreEqual(RewriteFixture.Udi(RewriteFixture.Copy), result);
        Assert.IsEmpty(context.ExternalReferences);
    }

    [TestMethod]
    [DataRow("umb://media/11111111111111111111111111111111")]
    [DataRow("rubbish")]
    public void Ignores_anything_that_is_not_a_document_udi(string value)
        => Assert.IsNull(_rewriter.Rewrite(value, RewriteFixture.Context()));
}

[TestClass]
public class MultiNodeTreePickerRewriterTests
{
    private readonly MultiNodeTreePickerRewriter _rewriter = new();

    [TestMethod]
    public void Repoints_only_the_document_entries_of_a_mixed_picker()
    {
        const string media = "umb://media/99999999999999999999999999999999";
        const string member = "umb://member/88888888888888888888888888888888";
        RewriteContext context = RewriteFixture.Context();

        string value = string.Join(',',
            RewriteFixture.Udi(RewriteFixture.Original),
            media,
            RewriteFixture.Udi(RewriteFixture.Outside),
            member,
            RewriteFixture.Udi(RewriteFixture.SecondOriginal));

        string? result = _rewriter.Rewrite(value, context);

        Assert.AreEqual(
            string.Join(',',
                RewriteFixture.Udi(RewriteFixture.Copy),
                media,
                RewriteFixture.Udi(RewriteFixture.Outside),
                member,
                RewriteFixture.Udi(RewriteFixture.SecondCopy)),
            result);
        Assert.AreEqual(2, context.RewrittenLinkCount);
        Assert.ContainsSingle(context.ExternalReferences);
    }

    [TestMethod]
    public void Returns_null_when_nothing_in_the_value_was_copied()
    {
        string value = string.Join(',', RewriteFixture.Udi(RewriteFixture.Outside), "umb://media/7777");

        Assert.IsNull(_rewriter.Rewrite(value, RewriteFixture.Context()));
    }
}

[TestClass]
public class MultiUrlPickerRewriterTests
{
    private readonly MultiUrlPickerRewriter _rewriter = new();

    [TestMethod]
    public void Repoints_internal_links_and_leaves_external_ones()
    {
        RewriteContext context = RewriteFixture.Context();
        string value = $$"""
            [{"name":"Inside","target":"_blank","udi":"{{RewriteFixture.Udi(RewriteFixture.Original)}}","queryString":"?a=1"},
             {"name":"Elsewhere","udi":"{{RewriteFixture.Udi(RewriteFixture.Outside)}}"},
             {"name":"Google","url":"https://google.com"}]
            """;

        string? result = _rewriter.Rewrite(value, context);

        Assert.IsNotNull(result);
        Assert.Contains(RewriteFixture.Udi(RewriteFixture.Copy), result);
        Assert.Contains(RewriteFixture.Udi(RewriteFixture.Outside), result);
        Assert.Contains("https://google.com", result);
        // Fields TrueCopy knows nothing about survive, because it edits the JSON tree rather than
        // round-tripping through a model.
        Assert.Contains("?a=1", result);
        Assert.Contains("_blank", result);
        Assert.AreEqual(1, context.RewrittenLinkCount);
        Assert.ContainsSingle(context.ExternalReferences);
    }

    [TestMethod]
    public void Handles_the_legacy_type_and_unique_pairing()
    {
        RewriteContext context = RewriteFixture.Context();
        string value = $$"""[{"name":"Old","type":"document","unique":"{{RewriteFixture.Original}}"}]""";

        string? result = _rewriter.Rewrite(value, context);

        Assert.IsNotNull(result);
        Assert.Contains(RewriteFixture.Copy.ToString(), result);
    }

    [TestMethod]
    [DataRow("{ not json")]
    [DataRow("{\"notAnArray\":true}")]
    [DataRow("[{\"udi\":42}]")]
    [DataRow("[null]")]
    public void Never_throws_on_a_value_it_cannot_read(string value)
        => Assert.IsNull(_rewriter.Rewrite(value, RewriteFixture.Context()));
}
