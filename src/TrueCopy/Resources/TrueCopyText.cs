using System.Globalization;
using System.Resources;
using Umbraco.Cms.Core.Models.Membership;

namespace Our.Umbraco.TrueCopy.Resources
{
    /// <summary>
    ///     The package's server-side strings, in the language of whoever is asking.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Only the strings that cannot be localized in the browser live here. Everything an element
    ///         renders comes from the client dictionary in <c>Client/src/lang</c> instead; these are the
    ///         ProblemDetails the management API returns, which core renders generically from the
    ///         response body, so by the time our TypeScript could see them they are already prose.
    ///     </para>
    ///     <para>
    ///         Adding a language is one more <c>TrueCopyResources.&lt;culture&gt;.resx</c> beside the
    ///         English one - .NET builds it into a satellite assembly and resolves it through the normal
    ///         fallback chain, so <c>nl-NL</c> finds <c>nl</c>, and anything with no translation at all
    ///         finds English.
    ///     </para>
    /// </remarks>
    internal static class TrueCopyText
    {
        private static readonly ResourceManager Resources = new(
            $"{typeof(TrueCopyText).Namespace}.TrueCopyResources",
            typeof(TrueCopyText).Assembly);

        /// <summary>
        ///     Resolves a backoffice user's <see cref="IUser.Language" />
        ///     to a culture, falling back to the invariant culture rather than throwing.
        /// </summary>
        /// <remarks>
        ///     The value comes from the user's profile and is not validated on the way in, so a stale or
        ///     hand-edited row can hold something that is not a culture name at all.
        /// </remarks>
        public static CultureInfo CultureFor(string? language)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                return CultureInfo.InvariantCulture;
            }

            try
            {
                return CultureInfo.GetCultureInfo(language);
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.InvariantCulture;
            }
        }

        /// <summary>Looks up a string, falling back to the key so a missing entry is obvious rather than blank.</summary>
        public static string Get(string name, CultureInfo culture)
            => Resources.GetString(name, culture) ?? name;

        /// <summary>Looks up a string and fills in its placeholders, formatting them for the same culture.</summary>
        public static string Get(string name, CultureInfo culture, params object?[] arguments)
            => string.Format(culture, Get(name, culture), arguments);
    }
}
