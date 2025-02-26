using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Script.Util.Database
{
    public static class CsvHandler
    {
        public static string ConvertCsvToJson(string csv)
        {
            string[] lines = csv.Split("\r\n");

            if (lines.Length < 2)
                throw new Exception("CSV file must have a header and at least one row");

            string[] headers = lines[0].Split(',');
            StringBuilder jsonBuilder = new();
            jsonBuilder.Append('[');

            for (int i = 1; i < lines.Length; i++)
            {
                string[] values = lines[i].Split(',');
                jsonBuilder.Append('{');

                for (int j = 0; j < headers.Length; j++)
                {
                    jsonBuilder.Append($"\"{headers[j].Trim()}\": \"{values[j].Trim()}\"");
                    if (j < headers.Length - 1)
                        jsonBuilder.Append(", ");
                }

                jsonBuilder.Append('}');
                if (i < lines.Length - 1)
                    jsonBuilder.Append(", ");
            }

            jsonBuilder.Append(']');
            return jsonBuilder.ToString();
        }

        public static string ConvertJsonToCsv(string json)
        {
            JsonDocument jsonDocument = JsonDocument.Parse(json);
            JsonElement root = jsonDocument.RootElement;

            if (root.ValueKind != JsonValueKind.Array) return string.Empty;

            StringWriter csvBuilder = new();

            HashSet<string> headers = [];
            List<List<string>> rows = [];

            foreach (JsonElement item in root.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) break;

                List<string> row = [];

                foreach (JsonProperty property in item.EnumerateObject())
                {
                    headers.Add(property.Name);
                    row.Add(GetValue(property.Value));
                }

                rows.Add(row);
            }

            WriteCsvRow(csvBuilder, headers);

            foreach (List<string> row in rows) WriteCsvRow(csvBuilder, headers.Select(header => row.ElementAtOrDefault(GetIndex(header, headers))));

            csvBuilder.Flush();
            return csvBuilder.ToString();
        }

        private static int GetIndex(string header, IEnumerable<string> headers)
        {
            int index = headers.ToList().IndexOf(header);
            return index;
        }

        private static void WriteCsvRow(TextWriter writer, IEnumerable<string?>? fields)
        {
            if (fields is null) return;

            string row = string.Join(",", fields.Select(QuoteField));
            writer.WriteLine(row);
        }

        private static string GetValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number => element.GetDecimal().ToString(CultureInfo.InvariantCulture),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => string.Empty,
                _ => string.Empty
            };
        }

        private static string? QuoteField(string? field)
        {
            if (string.IsNullOrEmpty(field)) return field;

            bool needsQuotes = field.Contains(',') || field.Contains('"') || field.Contains('\n');
            if (!needsQuotes) return field;

            field = field.Replace("\"", "\"\"");
            return $"\"{field}\"";
        }
    }
}
