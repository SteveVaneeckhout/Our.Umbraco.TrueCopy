using System.Text.Json;
using System.Text.Json.Nodes;

namespace Our.Umbraco.TrueCopy.Rewriting;

/// <summary>
///     Small guards over <c>JsonNode</c>. Every rewriter works on the JSON tree rather than a typed model,
///     so that fields TrueCopy knows nothing about survive a rewrite unchanged, and so that a value whose
///     shape is not what we expected is simply left alone instead of throwing.
/// </summary>
public static class JsonRewriting
{
    /// <summary>Parses, returning null rather than throwing on anything malformed.</summary>
    /// <param name="value">The stored property value.</param>
    /// <returns>The parsed node, or <c>null</c> if it was not valid JSON.</returns>
    public static JsonNode? TryParse(string value)
    {
        try
        {
            return JsonNode.Parse(value);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Reads a string property, or null if it is absent, null, or not a string.</summary>
    /// <param name="obj">The object to read from.</param>
    /// <param name="propertyName">The property name.</param>
    /// <returns>The string value, or <c>null</c>.</returns>
    public static string? GetString(this JsonObject obj, string propertyName)
        => obj[propertyName] is JsonValue value && value.TryGetValue(out string? text) ? text : null;
}
