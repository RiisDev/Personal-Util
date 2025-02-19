using System.Text.RegularExpressions;

namespace Script.Util
{
    public static class RegexExpander
    {
        public static string ExtractValue(this Match match, int group = 0) => match.Groups[group].Value;
    }
}
