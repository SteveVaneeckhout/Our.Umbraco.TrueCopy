using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using UmbConstants = Umbraco.Cms.Core.Constants;

namespace Our.Umbraco.TrueCopy.Rewriting;

/// <summary>
///     <c>Umbraco.RichText</c> - <c>{"markup":"...","blocks":{...}}</c>.
/// </summary>
/// <remarks>
///     Internal links are <c>{localLink:...}</c> tokens inside an anchor's <c>href</c>. Three forms exist
///     in real data and all three are handled: the Umbraco 17 bare GUID, the older
///     <c>umb://document/&lt;guid&gt;</c> UDI, and the pre-v7 integer id. Each is rewritten back into the
///     form it arrived in.
///     <para>
///         The match is anchored on the <c>&lt;a&gt;</c> tag rather than on the token alone so the tag's
///         <c>type</c> attribute can be read. A <c>{localLink:&lt;guid&gt;}</c> with <c>type="media"</c> is
///         a media link: its GUID will never be in the copy map, so rewriting it is a no-op either way,
///         but reporting it as a document reference left pointing outside the copy would be a lie.
///     </para>
///     <para>
///         Blocks embedded in rich text use the same structure as a Block List, so that half is delegated
///         to <see cref="BlockEditorRewriter" />. Block placeholders in the markup
///         (<c>&lt;umb-rte-block data-content-key="..."&gt;</c>) reference block content keys, not
///         documents, and are left alone.
///     </para>
/// </remarks>
public sealed partial class RichTextRewriter : ILinkRewriter
{
    public bool CanRewrite(string propertyEditorAlias)
        => propertyEditorAlias == UmbConstants.PropertyEditors.Aliases.RichText;

    public string? Rewrite(string value, RewriteContext context)
    {
        // Umbraco 17 always stores the JSON envelope, but a value carried over from an older install can
        // still be bare HTML. Treat anything that is not the envelope as markup on its own.
        if (JsonRewriting.TryParse(value) is not JsonObject root)
        {
            string? rewrittenMarkup = RewriteMarkup(value, context);
            return rewrittenMarkup;
        }

        var changed = false;

        if (root.GetString("markup") is { } markup)
        {
            string? rewritten = RewriteMarkup(markup, context);
            if (rewritten is not null)
            {
                root["markup"] = rewritten;
                changed = true;
            }
        }

        if (root["blocks"] is JsonObject blocks)
        {
            changed |= BlockEditorRewriter.RewriteBlocks(blocks, context);
        }

        return changed ? root.ToJsonString() : null;
    }

    /// <summary>Rewrites the local links in a fragment of markup, or returns null if none changed.</summary>
    private static string? RewriteMarkup(string markup, RewriteContext context)
    {
        if (markup.IndexOf("localLink:", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return null;
        }

        var changed = false;

        string result = AnchorTagRegex().Replace(markup, anchor =>
        {
            string tag = anchor.Value;

            Match type = LinkTypeRegex().Match(tag);
            if (type.Success && type.Groups["type"].Value.Equals("media", StringComparison.OrdinalIgnoreCase))
            {
                return tag;
            }

            return LocalLinkRegex().Replace(tag, token =>
            {
                string? replacement = RewriteToken(token.Groups["id"].Value, context);
                if (replacement is null)
                {
                    return token.Value;
                }

                changed = true;
                return $"{token.Groups["open"].Value}localLink:{replacement}{token.Groups["close"].Value}";
            });
        });

        return changed ? result : null;
    }

    /// <summary>Rewrites one token's payload, preserving whichever of the three forms it arrived in.</summary>
    private static string? RewriteToken(string id, RewriteContext context)
    {
        if (DocumentUdi.TryParse(id, out Guid? udiTarget))
        {
            Guid? copy = context.ResolveDocument(udiTarget.Value);
            return copy is null ? null : DocumentUdi.Format(copy.Value);
        }

        if (Guid.TryParse(id, out Guid guidTarget))
        {
            Guid? copy = context.ResolveDocument(guidTarget);
            return copy?.ToString();
        }

        if (int.TryParse(id, out int intTarget))
        {
            int? copy = context.ResolveDocumentId(intTarget);
            return copy?.ToString();
        }

        return null;
    }

    [GeneratedRegex("<a\\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture)]
    private static partial Regex AnchorTagRegex();

    [GeneratedRegex("type=['\"](?<type>media|document)['\"]", RegexOptions.IgnoreCase)]
    private static partial Regex LinkTypeRegex();

    [GeneratedRegex("(?<open>\\{|%7B)localLink:(?<id>[^}%'\"]+)(?<close>\\}|%7D)", RegexOptions.IgnoreCase)]
    private static partial Regex LocalLinkRegex();
}
