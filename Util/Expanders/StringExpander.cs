using System.Text;
using System.Web;
using static System.Text.RegularExpressions.Regex;

namespace Script.Util.Expanders
{
    public static class StringExpander
    {
        public static string TryExtractSubstring(this string log, string startToken, char endToken, Func<int, bool> condition, string prefix = " ")
        {
            int startIndex = log.IndexOf(startToken, StringComparison.Ordinal);
            int endIndex = log.IndexOf(endToken, startIndex);
            return startIndex != -1 && endIndex != -1 && condition(startIndex) ? log.Substring(startIndex, endIndex - startIndex).Replace(prefix, "") : "";
        }

        public static string ToBase64(this string value) => Convert.ToBase64String(Encoding.ASCII.GetBytes(value));

        public static string FromBase64(this string value)
        {
            try
            {
                if (value.Contains('<')) value = value[..value.IndexOf('<')];
                return Encoding.ASCII.GetString(Convert.FromBase64String(value));
            }
            catch { return ""; }

        }
        
        public static bool IsNullOrEmpty(this string? text) => string.IsNullOrEmpty(text);

        public static string Normalize(this string abstractText)
        {
            if (string.IsNullOrEmpty(abstractText)) return abstractText;
            string decoded = HttpUtility.HtmlDecode(abstractText);
            string normalized = decoded.Normalize(NormalizationForm.FormKD);
            string unescaped = Unescape(normalized);
            unescaped = Replace(unescaped, @"[‘’]", "'")
                .Replace("”", "\"").Replace("“", "\"")
                .Replace("–", "-").Replace("—", "-")
                .Replace("\u00d7", "x")
                .Replace("  ", " ")
                .Trim();
            return unescaped;
        }

        public static string SafeFileName(this string abstractFile)
        {
            string normalizedText = abstractFile.Normalize();
            char[] invalidChars = Path.GetInvalidFileNameChars();
            string sanitized = new(normalizedText.Where(character => !invalidChars.Contains(character)).ToArray());

            sanitized = Replace(sanitized, @"\s+", "_");
            sanitized = Replace(sanitized, "_+", "_");

            return sanitized.Trim().Trim('_');
        }
    }
}
