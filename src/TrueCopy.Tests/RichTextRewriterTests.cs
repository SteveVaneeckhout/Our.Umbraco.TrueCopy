using System.Text.Json.Nodes;
using Our.Umbraco.TrueCopy.Rewriting;

namespace Our.Umbraco.TrueCopy.Tests;

[TestClass]
public class RichTextRewriterTests
{
    private readonly RichTextRewriter _rewriter = new();

    private static string Envelope(string markup)
        => new JsonObject
        {
            ["markup"] = markup,
            ["blocks"] = new JsonObject
            {
                ["contentData"] = new JsonArray(),
                ["settingsData"] = new JsonArray(),
                ["expose"] = new JsonArray(),
                ["layout"] = new JsonObject(),
            },
        }.ToJsonString();

    private static string MarkupOf(string? value)
        => (JsonNode.Parse(value!) as JsonObject)!.GetString("markup")!;

    private static string LocalLink(object id) => "{localLink:" + id + "}";

    private static string Anchor(object id, string? type = null)
    {
        string typeAttribute = type is null ? string.Empty : $" type=\"{type}\"";
        return $"<a href=\"/{LocalLink(id)}\"{typeAttribute}>Go</a>";
    }

    [TestMethod]
    public void Rewrites_the_Umbraco_18_guid_form()
    {
        RewriteContext context = RewriteFixture.Context();
        string value = Envelope($"<p>{Anchor(RewriteFixture.Original, "document")}</p>");

        string? result = _rewriter.Rewrite(value, context);

        Assert.IsNotNull(result);
        Assert.Contains(LocalLink(RewriteFixture.Copy), MarkupOf(result));
        // The form it arrived in is the form it leaves in - a bare guid stays a bare guid.
        Assert.DoesNotContain("umb://", MarkupOf(result));
        Assert.AreEqual(1, context.RewrittenLinkCount);
    }

    [TestMethod]
    public void Rewrites_the_legacy_udi_form_and_keeps_it_a_udi()
    {
        RewriteContext context = RewriteFixture.Context();
        string value = Envelope(Anchor(RewriteFixture.Udi(RewriteFixture.Original)));

        string? result = _rewriter.Rewrite(value, context);

        Assert.IsNotNull(result);
        Assert.Contains(LocalLink(RewriteFixture.Udi(RewriteFixture.Copy)), MarkupOf(result));
    }

    [TestMethod]
    public void Rewrites_the_pre_v7_integer_form_through_the_id_map()
    {
        RewriteContext context = RewriteFixture.Context(new Dictionary<int, int> { [1234] = 5678 });

        string? result = _rewriter.Rewrite(Envelope(Anchor(1234)), context);

        Assert.IsNotNull(result);
        Assert.Contains(LocalLink(5678), MarkupOf(result));
    }

    [TestMethod]
    public void Leaves_a_media_link_alone_and_does_not_report_it_as_a_document_reference()
    {
        // A media guid is never in the copy map, so rewriting it is a no-op either way - but reporting it
        // as a document link left pointing outside the copy would be plainly wrong.
        RewriteContext context = RewriteFixture.Context();

        Assert.IsNull(_rewriter.Rewrite(Envelope(Anchor(Guid.NewGuid(), "media")), context));
        Assert.IsEmpty(context.ExternalReferences);
    }

    [TestMethod]
    public void Reports_a_document_link_pointing_outside_the_copy()
    {
        RewriteContext context = RewriteFixture.Context();

        Assert.IsNull(_rewriter.Rewrite(Envelope(Anchor(RewriteFixture.Outside, "document")), context));
        Assert.AreEqual(RewriteFixture.Outside, Assert.ContainsSingle(context.ExternalReferences).TargetKey);
    }

    [TestMethod]
    public void Leaves_block_placeholders_in_the_markup_alone()
    {
        // data-content-key identifies a block, not a document. The deep clone keeps it consistent with
        // the layout, so rewriting it would detach the block from its own layout entry.
        RewriteContext context = RewriteFixture.Context();
        string placeholder = $"<umb-rte-block data-content-key=\"{RewriteFixture.Original}\"></umb-rte-block>";

        string? result = _rewriter.Rewrite(
            Envelope(placeholder + Anchor(RewriteFixture.Original, "document")), context);

        Assert.IsNotNull(result);
        Assert.Contains(placeholder, MarkupOf(result));
        Assert.Contains(LocalLink(RewriteFixture.Copy), MarkupOf(result));
    }

    [TestMethod]
    public void Rewrites_a_picker_in_a_block_embedded_in_the_rich_text()
    {
        RewriteContext context = RewriteFixture.Context();
        var value = new JsonObject
        {
            ["markup"] = "<p>Words</p>",
            ["blocks"] = new JsonObject
            {
                ["contentData"] = new JsonArray(new JsonObject
                {
                    ["key"] = "cccccccc-cccc-cccc-cccc-cccccccccccc",
                    ["contentTypeKey"] = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                    ["values"] = new JsonArray(new JsonObject
                    {
                        ["alias"] = "link",
                        ["value"] = RewriteFixture.Udi(RewriteFixture.Original),
                        ["editorAlias"] = "Umbraco.ContentPicker",
                    }),
                }),
                ["settingsData"] = new JsonArray(),
                ["expose"] = new JsonArray(),
                ["layout"] = new JsonObject(),
            },
        }.ToJsonString();

        string? result = _rewriter.Rewrite(value, context);

        Assert.IsNotNull(result);
        Assert.Contains(RewriteFixture.Udi(RewriteFixture.Copy), result);
        Assert.AreEqual(1, context.RewrittenLinkCount);
    }

    [TestMethod]
    public void Returns_null_when_the_markup_holds_no_local_links()
        => Assert.IsNull(_rewriter.Rewrite(Envelope("<p>Just words.</p>"), RewriteFixture.Context()));

    [TestMethod]
    public void Handles_bare_html_left_over_from_an_older_install()
    {
        RewriteContext context = RewriteFixture.Context();

        string? result = _rewriter.Rewrite(Anchor(RewriteFixture.Original, "document"), context);

        Assert.IsNotNull(result);
        Assert.Contains(LocalLink(RewriteFixture.Copy), result);
    }
}
