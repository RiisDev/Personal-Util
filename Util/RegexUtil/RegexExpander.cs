using System.Diagnostics.CodeAnalysis;

namespace Script.Util.RegexUtil
{
    public static class RegexExpander
    {
        public static bool RegexEndsWith(this string text, [StringSyntax(StringSyntaxAttribute.Regex)] string pattern) => Regex.IsMatch(text, pattern + "$");

        public static bool RegexContains(this string text, [StringSyntax(StringSyntaxAttribute.Regex)] string pattern) => Regex.IsMatch(text, pattern);

		public static string ExtractValue(this Match match, int groupId = 0) => match is not { Success: true } ? "" : match.Groups[groupId].Value.Replace("\r", "").Replace("\n", "");
        
        public static string RegexReplace(this string text, [StringSyntax(StringSyntaxAttribute.Regex)] string pattern, string replacement)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (string.IsNullOrEmpty(pattern)) return text;
            return replacement is null ? throw new ArgumentNullException(nameof(replacement)) : Regex.Replace(text, pattern, replacement);
        }
    }
}
