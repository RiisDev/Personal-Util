using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Script.Util.Regex
{
    public static class RegexExpander
    {
        public static bool RegexEndsWith(this string text, [StringSyntax(StringSyntaxAttribute.Regex)] string pattern) => System.Text.RegularExpressions.Regex.IsMatch(text, pattern + "$");
        public static string ExtractValue(this Match match, int groupId = 0) => match is not { Success: true } ? "" : match.Groups[groupId].Value.Replace("\r", "").Replace("\n", "");
    }
}
