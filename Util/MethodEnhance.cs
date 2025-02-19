using System.Text;
using System.Web;
using static System.Text.RegularExpressions.Regex;
#pragma warning disable SYSLIB1045

namespace Script.Util
{
    public static class MethodEnhance
    {
        public static bool TryAdd<T>(this List<T> list, T value)
        {
            if (list.Contains(value)) return false;
            list.Add(value);
            return true;
        }
        
        public static bool IsNullOrEmpty(this string text) => string.IsNullOrEmpty(text);

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
            string sanitized = new (normalizedText.Where(character => !invalidChars.Contains(character)).ToArray());

            sanitized = Replace(sanitized, @"\s+", "_");
            sanitized = Replace(sanitized, "_+", "_");

            return sanitized.Trim().Trim('_');

        }
    }
}
