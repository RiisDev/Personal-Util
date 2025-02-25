using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Web;
using static System.Text.RegularExpressions.Regex;
#pragma warning disable SYSLIB1045

namespace Script.Util
{
    public static class MethodEnhance
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true,
            IncludeFields = true,
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
        };

        public static bool TryAdd<T>(this List<T> list, T value)
        {
            if (list.Contains(value)) return false;
            list.Add(value);
            return true;
        }

        public static bool RegexEndsWith(this string text, [StringSyntax(StringSyntaxAttribute.Regex)] string pattern) => IsMatch(text, pattern + "$");

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

        // Dotnet devs can suck my fucking balls
        // https://github.com/dotnet/runtime/issues/1174
        public static string JsonSerialize<TValue>(TValue obj, bool @unsafe = true)
        {
            string jsonBody = JsonSerializer.Serialize(obj, SerializerOptions);

            StringBuilder newBody = new(jsonBody.Length);
            
            bool insideValue = false;

            for (int i = 0; i < jsonBody.Length; i++)
            {
                if (jsonBody[i] == '"') insideValue = !insideValue;

                if (i + 1 < jsonBody.Length && jsonBody[i] == ' ' && jsonBody[i + 1] == ' ' && !insideValue)
                {
                    int indentCount = 0;

                    while (i + indentCount < jsonBody.Length && jsonBody[i + indentCount] == ' ') indentCount++;

                    int tabCount = indentCount / 2;
                    newBody.Append(new string('\t', tabCount));
                    i += indentCount - 1;
                }
                else newBody.Append(jsonBody[i]);
            }

            return newBody.ToString();
        }
    }
}
