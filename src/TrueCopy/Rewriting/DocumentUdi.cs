using System.Diagnostics.CodeAnalysis;

namespace Our.Umbraco.TrueCopy.Rewriting;

/// <summary>
///     Reads and writes the <c>umb://document/&lt;guid&gt;</c> strings that most pickers store.
/// </summary>
/// <remarks>
///     Hand-rolled rather than going through <c>UdiParser</c>/<c>GuidUdi</c> because parsing a UDI depends
///     on the static UDI type registry, which is populated during boot - and every rewriter in this
///     package is meant to be exercisable without an Umbraco host. The format is fixed and trivial, and
///     <c>DocumentUdiTests</c> asserts that what we produce is byte-identical to <c>GuidUdi.ToString()</c>.
///     Only document UDIs are recognised. Media and member references pass through the pickers untouched,
///     which is correct: TrueCopy copies documents, so nothing else has moved.
/// </remarks>
public static class DocumentUdi
{
    /// <summary>The scheme and entity type every document UDI starts with.</summary>
    public const string Prefix = "umb://document/";

    /// <summary>Parses a document UDI, returning false for anything else - media, members, rubbish.</summary>
    /// <param name="value">The stored value, which may be null, blank or malformed.</param>
    /// <param name="key">The document key, when the value was a document UDI.</param>
    /// <returns><c>true</c> if <paramref name="value"/> was a document UDI.</returns>
    public static bool TryParse(string? value, [NotNullWhen(true)] out Guid? key)
    {
        key = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        ReadOnlySpan<char> span = value.AsSpan().Trim();
        if (!span.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!Guid.TryParse(span[Prefix.Length..], out Guid parsed))
        {
            return false;
        }

        key = parsed;
        return true;
    }

    /// <summary>Writes a document key back out in the form Umbraco stores.</summary>
    /// <param name="key">The document key.</param>
    /// <returns>The <c>umb://document/&lt;guid&gt;</c> string.</returns>
    public static string Format(Guid key) => Prefix + key.ToString("N");
}
