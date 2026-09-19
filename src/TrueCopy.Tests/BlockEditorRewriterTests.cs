using System.Text.Json.Nodes;
using Our.Umbraco.TrueCopy.Rewriting;

namespace Our.Umbraco.TrueCopy.Tests;

[TestClass]
public class BlockEditorRewriterTests
{
    private static readonly Guid ElementTypeKey = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly BlockEditorRewriter _rewriter = new();

    private static string BlockList(JsonArray contentData, JsonArray? settingsData = null)
        => new JsonObject
        {
            ["layout"] = new JsonObject
            {
                ["Umbraco.BlockList"] = new JsonArray(
                    new JsonObject { ["contentKey"] = "cccccccc-cccc-cccc-cccc-cccccccccccc" }),
            },
            ["contentData"] = contentData,
            ["settingsData"] = settingsData ?? new JsonArray(),
            ["expose"] = new JsonArray(
                new JsonObject { ["contentKey"] = "cccccccc-cccc-cccc-cccc-cccccccccccc", ["culture"] = null }),
        }.ToJsonString();

    private static JsonObject Block(params JsonObject[] values)
        => new()
        {
            ["key"] = "cccccccc-cccc-cccc-cccc-cccccccccccc",
            ["contentTypeKey"] = ElementTypeKey.ToString(),
            ["values"] = new JsonArray(values.Cast<JsonNode?>().ToArray()),
        };

    private static JsonObject Value(string alias, JsonNode? value, string? editorAlias)
    {
        var property = new JsonObject { ["alias"] = alias, ["value"] = value, ["culture"] = null, ["segment"] = null };
        if (editorAlias is not null)
        {
            property["editorAlias"] = editorAlias;
        }

        return property;
    }

    [TestMethod]
    public void Rewrites_a_picker_nested_in_a_block()
    {
        RewriteContext context = RewriteFixture.Context();
        string value = BlockList(new JsonArray(
            Block(Value("link", RewriteFixture.Udi(RewriteFixture.Original), "Umbraco.ContentPicker"))));

        string? result = _rewriter.Rewrite(value, context);

        Assert.IsNotNull(result);
        Assert.Contains(RewriteFixture.Udi(RewriteFixture.Copy), result);
        Assert.AreEqual(1, context.RewrittenLinkCount);
    }

    [TestMethod]
    public void Rewrites_a_picker_in_settings_data_too()
    {
        RewriteContext context = RewriteFixture.Context();
        string value = BlockList(
            new JsonArray(),
            new JsonArray(Block(Value("link", RewriteFixture.Udi(RewriteFixture.Original), "Umbraco.ContentPicker"))));

        Assert.IsNotNull(_rewriter.Rewrite(value, context));
        Assert.AreEqual(1, context.RewrittenLinkCount);
    }

    [TestMethod]
    public void Recurses_into_a_block_nested_in_a_block()
    {
        RewriteContext context = RewriteFixture.Context();

        JsonNode inner = JsonNode.Parse(BlockList(new JsonArray(
            Block(Value("deepLink", RewriteFixture.Udi(RewriteFixture.Original), "Umbraco.ContentPicker")))))!;

        string value = BlockList(new JsonArray(Block(Value("innerBlocks", inner, "Umbraco.BlockList"))));

        string? result = _rewriter.Rewrite(value, context);

        Assert.IsNotNull(result);
        Assert.Contains(RewriteFixture.Udi(RewriteFixture.Copy), result);
        Assert.AreEqual(1, context.RewrittenLinkCount);

        // A nested block value is stored as inline JSON, and must come back as inline JSON rather than a
        // JSON-encoded string - Umbraco could not read the latter.
        JsonObject root = (JsonObject)JsonNode.Parse(result)!;
        JsonNode? rewrittenInner = root["contentData"]![0]!["values"]![0]!["value"];
        Assert.IsInstanceOfType<JsonObject>(rewrittenInner);
    }

    [TestMethod]
    public void Keeps_layout_expose_and_the_block_keys_untouched()
    {
        RewriteContext context = RewriteFixture.Context();
        string value = BlockList(new JsonArray(
            Block(Value("link", RewriteFixture.Udi(RewriteFixture.Original), "Umbraco.ContentPicker"))));

        string? result = _rewriter.Rewrite(value, context);

        Assert.IsNotNull(result);
        JsonObject before = (JsonObject)JsonNode.Parse(value)!;
        JsonObject after = (JsonObject)JsonNode.Parse(result)!;

        Assert.AreEqual(before["layout"]!.ToJsonString(), after["layout"]!.ToJsonString());
        Assert.AreEqual(before["expose"]!.ToJsonString(), after["expose"]!.ToJsonString());
        Assert.AreEqual(
            before["contentData"]![0]!["contentTypeKey"]!.GetValue<string>(),
            after["contentData"]![0]!["contentTypeKey"]!.GetValue<string>());
        Assert.AreEqual(
            before["contentData"]![0]!["key"]!.GetValue<string>(),
            after["contentData"]![0]!["key"]!.GetValue<string>());
    }

    [TestMethod]
    public void Falls_back_to_the_content_type_when_editorAlias_is_missing()
    {
        // Values written by an older Umbraco carry no editorAlias. Guessing the editor from the value's
        // shape is how a rewriter starts corrupting data, so the element type is consulted instead.
        var resolved = new List<(Guid, string)>();
        RewriteContext context = RewriteFixture.Context(nestedEditorAliasResolver: (typeKey, alias) =>
        {
            resolved.Add((typeKey, alias));
            return "Umbraco.ContentPicker";
        });

        string value = BlockList(new JsonArray(
            Block(Value("link", RewriteFixture.Udi(RewriteFixture.Original), editorAlias: null))));

        string? result = _rewriter.Rewrite(value, context);

        Assert.IsNotNull(result);
        Assert.Contains(RewriteFixture.Udi(RewriteFixture.Copy), result);
        Assert.AreEqual((ElementTypeKey, "link"), Assert.ContainsSingle(resolved));
    }

    [TestMethod]
    public void Skips_a_value_whose_editor_cannot_be_determined()
    {
        RewriteContext context = RewriteFixture.Context();
        string value = BlockList(new JsonArray(
            Block(Value("link", RewriteFixture.Udi(RewriteFixture.Original), editorAlias: null))));

        Assert.IsNull(_rewriter.Rewrite(value, context));
    }

    [TestMethod]
    public void Returns_null_when_no_nested_value_pointed_at_a_copy()
    {
        RewriteContext context = RewriteFixture.Context();
        string value = BlockList(new JsonArray(
            Block(Value("link", RewriteFixture.Udi(RewriteFixture.Outside), "Umbraco.ContentPicker"))));

        Assert.IsNull(_rewriter.Rewrite(value, context));
        Assert.ContainsSingle(context.ExternalReferences);
    }

    [TestMethod]
    [DataRow("{ not json")]
    [DataRow("[]")]
    [DataRow("{\"contentData\":\"nonsense\"}")]
    [DataRow("{\"contentData\":[{\"values\":42}]}")]
    public void Never_throws_on_a_value_it_cannot_read(string value)
        => Assert.IsNull(_rewriter.Rewrite(value, RewriteFixture.Context()));
}
