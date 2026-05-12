using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

public sealed class CsvToSqlConverter
{
	public string TableName { get; init; } = "ImportedData";
	public bool IncludeCreateTable { get; init; } = true;
	public bool IncludeDropTable { get; init; } = false;
	public bool UseNullableColumns { get; init; } = true;
	public int TypeInferenceSampleRows { get; init; } = 200;
	public string NullPlaceholder { get; init; } = "";
	public int VarcharPaddingPercent { get; init; } = 20;

	public string Convert(string csvContent)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(csvContent);

		string[] lines = SplitLines(csvContent);
		if (lines.Length < 2)
			throw new InvalidDataException("CSV must contain at least a header row and one data row.");

		char delimiter = DetectDelimiter(lines);
		string[] headers = ParseLine(lines[0], delimiter);
		List<string[]> rows = ParseRows(lines.Skip(1), delimiter, headers.Length);

		ColumnInfo[] columns = InferColumnTypes(headers, rows);

		StringBuilder sql = new();

		if (IncludeDropTable)
			sql.AppendLine($"DROP TABLE IF EXISTS [{TableName}];").AppendLine();

		if (IncludeCreateTable)
		{
			sql.AppendLine(BuildCreateTable(columns));
			sql.AppendLine();
		}

		sql.AppendLine(BuildInsertStatements(columns, rows));

		return sql.ToString();
	}

	/// <summary>Converts a CSV file to SQL statements.</summary>
	public string ConvertFile(string filePath, System.Text.Encoding? encoding = null)
	{
		string content = File.ReadAllText(filePath, encoding ?? System.Text.Encoding.UTF8);
		return Convert(content);
	}

	private static readonly char[] CandidateDelimiters = [',', ';', '\t', '|', ':'];

	private static char DetectDelimiter(string[] lines)
	{
		int sampleCount = Math.Min(5, lines.Length);
		string[] sampleLines = lines.Take(sampleCount).ToArray();

		char bestDelimiter = ',';
		double bestScore = -1;

		foreach (char candidate in CandidateDelimiters)
		{
			int[] counts = sampleLines.Select(l => CountUnquotedOccurrences(l, candidate)).ToArray();
			int firstCount = counts[0];
			if (firstCount == 0) continue;

			double consistency = counts.Count(c => c == firstCount) / (double)counts.Length;
			double score = firstCount * consistency;

			if (score > bestScore)
			{
				bestScore = score;
				bestDelimiter = candidate;
			}
		}

		return bestDelimiter;
	}

	private static int CountUnquotedOccurrences(string line, char ch)
	{
		bool inQuotes = false;
		int count = 0;
		foreach (char c in line)
		{
			if (c == '"') inQuotes = !inQuotes;
			else if (c == ch && !inQuotes) count++;
		}
		return count;
	}

	private static string[] SplitLines(string content)
	{
		return content.ReplaceLineEndings("\n")
					  .Split('\n', StringSplitOptions.RemoveEmptyEntries);
	}

	private static string[] ParseLine(string line, char delimiter)
	{
		List<string> fields = [];
		bool inQuotes = false;
		StringBuilder current = new();

		for (int i = 0; i < line.Length; i++)
		{
			char c = line[i];

			if (c == '"')
			{
				if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
				{
					current.Append('"');
					i++;
				}
				else
				{
					inQuotes = !inQuotes;
				}
			}
			else if (c == delimiter && !inQuotes)
			{
				fields.Add(current.ToString().Trim());
				current.Clear();
			}
			else
			{
				current.Append(c);
			}
		}

		fields.Add(current.ToString().Trim());
		return [.. fields];
	}

	private static List<string[]> ParseRows(IEnumerable<string> lines, char delimiter, int expectedColumns)
	{
		List<string[]> rows = [];

		foreach (string line in lines)
		{
			if (string.IsNullOrWhiteSpace(line)) continue;

			string[] fields = ParseLine(line, delimiter);

			if (fields.Length < expectedColumns)
			{
				string[] padded = new string[expectedColumns];
				Array.Copy(fields, padded, fields.Length);
				for (int i = fields.Length; i < expectedColumns; i++)
					padded[i] = string.Empty;
				fields = padded;
			}
			else if (fields.Length > expectedColumns)
			{
				fields = fields[..expectedColumns];
			}

			rows.Add(fields);
		}

		return rows;
	}

	private enum SqlType { Bit, Int, BigInt, Decimal, Float, Date, DateTime, NVarChar }

	private sealed record ColumnInfo(string Name, SqlType Type, bool IsNullable, int MaxLength, int Precision, int Scale);

	private ColumnInfo[] InferColumnTypes(string[] headers, List<string[]> rows)
	{
		int columnCount = headers.Length;
		IEnumerable<string[]> sampleRows = rows.Take(TypeInferenceSampleRows);

		ColumnInfo[] columns = new ColumnInfo[columnCount];

		for (int col = 0; col < columnCount; col++)
		{
			IEnumerable<string> values = sampleRows.Select(r => r[col]);
			string cleanName = SanitizeIdentifier(headers[col], col);
			columns[col] = AnalyzeColumn(cleanName, values);
		}

		return columns;
	}

	private ColumnInfo AnalyzeColumn(string name, IEnumerable<string> values)
	{
		List<string> nonEmpty = values.Where(v => !IsNullValue(v)).ToList();
		bool hasNulls = values.Count() != nonEmpty.Count;
		bool isNullable = UseNullableColumns && hasNulls;

		if (nonEmpty.Count == 0)
			return new ColumnInfo(name, SqlType.NVarChar, true, 255, 0, 0);

		if (nonEmpty.All(IsBit))
			return new ColumnInfo(name, SqlType.Bit, isNullable, 0, 0, 0);

		if (nonEmpty.All(IsInt))
		{
			bool needsBigInt = nonEmpty.Any(v => !int.TryParse(v, out _));
			return new ColumnInfo(name, needsBigInt ? SqlType.BigInt : SqlType.Int, isNullable, 0, 0, 0);
		}

		if (nonEmpty.All(IsDecimal))
		{
			(int precision, int scale) = GetDecimalMetrics(nonEmpty);
			return new ColumnInfo(name, SqlType.Decimal, isNullable, 0, precision, scale);
		}

		if (nonEmpty.All(IsFloat))
			return new ColumnInfo(name, SqlType.Float, isNullable, 0, 0, 0);

		if (nonEmpty.All(IsDate))
			return new ColumnInfo(name, SqlType.Date, isNullable, 0, 0, 0);

		if (nonEmpty.All(IsDateTime))
			return new ColumnInfo(name, SqlType.DateTime, isNullable, 0, 0, 0);

		int maxLen = nonEmpty.Max(v => v.Length);
		int paddedLen = Math.Min(maxLen + (int)Math.Ceiling(maxLen * VarcharPaddingPercent / 100.0), 4000);
		int finalLen = paddedLen <= 8 ? 16 : paddedLen <= 50 ? 100 : paddedLen <= 255 ? 255 : paddedLen <= 1000 ? 1000 : 4000;

		return new ColumnInfo(name, SqlType.NVarChar, isNullable, finalLen, 0, 0);
	}

	private static bool IsBit(string v) =>
		v is "0" or "1" or "true" or "false" or "yes" or "no" or "t" or "f";

	private static bool IsInt(string v) => long.TryParse(v, out _);

	private static bool IsDecimal(string v) => decimal.TryParse(v,
		System.Globalization.NumberStyles.Number,
		System.Globalization.CultureInfo.InvariantCulture, out _);

	private static bool IsFloat(string v) => double.TryParse(v,
		System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowLeadingSign,
		System.Globalization.CultureInfo.InvariantCulture, out _);

	private static readonly string[] DateFormats = ["yyyy-MM-dd", "MM/dd/yyyy", "dd/MM/yyyy", "dd-MM-yyyy", "yyyy/MM/dd"];
	private static readonly string[] DateTimeFormats =
	[
		"yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "MM/dd/yyyy HH:mm:ss",
		"dd/MM/yyyy HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm:ssZ"
	];

	private static bool IsDate(string v) =>
		DateTimeOffset.TryParseExact(v, DateFormats,
			System.Globalization.CultureInfo.InvariantCulture,
			System.Globalization.DateTimeStyles.None, out _);

	private static bool IsDateTime(string v) =>
		DateTimeOffset.TryParseExact(v, DateTimeFormats,
			System.Globalization.CultureInfo.InvariantCulture,
			System.Globalization.DateTimeStyles.AssumeUniversal, out _);

	private static (int precision, int scale) GetDecimalMetrics(List<string> values)
	{
		int maxScale = 0;
		int maxPrecision = 0;

		foreach (string v in values)
		{
			int dotIndex = v.IndexOf('.');
			int intDigits = dotIndex < 0 ? v.TrimStart('-').Length : v[..dotIndex].TrimStart('-').Length;
			int fracDigits = dotIndex < 0 ? 0 : v[(dotIndex + 1)..].Length;
			maxScale = Math.Max(maxScale, fracDigits);
			maxPrecision = Math.Max(maxPrecision, intDigits + fracDigits);
		}

		return (Math.Min(maxPrecision + 2, 38), maxScale);
	}

	private string BuildCreateTable(ColumnInfo[] columns)
	{
		StringBuilder sb = new();
		sb.AppendLine($"CREATE TABLE [{TableName}] (");

		for (int i = 0; i < columns.Length; i++)
		{
			ColumnInfo col = columns[i];
			string sqlTypeDef = col.Type switch
			{
				SqlType.Bit => "BIT",
				SqlType.Int => "INT",
				SqlType.BigInt => "BIGINT",
				SqlType.Decimal => $"DECIMAL({col.Precision},{col.Scale})",
				SqlType.Float => "FLOAT",
				SqlType.Date => "DATE",
				SqlType.DateTime => "DATETIME2",
				SqlType.NVarChar => col.MaxLength >= 4000 ? "NVARCHAR(MAX)" : $"NVARCHAR({col.MaxLength})",
				_ => "NVARCHAR(255)"
			};

			string nullability = col.IsNullable ? "NULL" : "NOT NULL";
			string comma = i < columns.Length - 1 ? "," : "";
			sb.AppendLine($"    [{col.Name}] {sqlTypeDef} {nullability}{comma}");
		}

		sb.Append(");");
		return sb.ToString();
	}

	private string BuildInsertStatements(ColumnInfo[] columns, List<string[]> rows)
	{
		StringBuilder sb = new();
		string columnList = string.Join(", ", columns.Select(c => $"[{c.Name}]"));

		foreach (string[] row in rows)
		{
			string values = string.Join(", ", row.Select((value, i) => FormatValue(value, columns[i])));
			sb.AppendLine($"INSERT INTO [{TableName}] ({columnList}) VALUES ({values});");
		}

		return sb.ToString();
	}

	private string FormatValue(string raw, ColumnInfo col)
	{
		if (IsNullValue(raw)) return "NULL";

		return col.Type switch
		{
			SqlType.Bit => raw.ToLowerInvariant() is "true" or "yes" or "1" or "t" ? "1" : "0",
			SqlType.Int or SqlType.BigInt => raw,
			SqlType.Decimal or SqlType.Float => raw.Replace(',', '.'),
			SqlType.Date or SqlType.DateTime => $"'{EscapeSql(raw)}'",
			SqlType.NVarChar => $"N'{EscapeSql(raw)}'",
			_ => $"N'{EscapeSql(raw)}'"
		};
	}

	private bool IsNullValue(string value) =>
		string.IsNullOrWhiteSpace(value) || value.Equals(NullPlaceholder, StringComparison.OrdinalIgnoreCase)
			|| value.Equals("null", StringComparison.OrdinalIgnoreCase)
			|| value.Equals("n/a", StringComparison.OrdinalIgnoreCase)
			|| value.Equals("na", StringComparison.OrdinalIgnoreCase)
			|| value == "-";

	private static string EscapeSql(string value) => value.Replace("'", "''");

	private static string SanitizeIdentifier(string header, int fallbackIndex)
	{
		if (string.IsNullOrWhiteSpace(header))
			return $"Column{fallbackIndex + 1}";

		string sanitized = Regex.Replace(header.Trim(), @"[^\w]", "_");

		if (char.IsDigit(sanitized[0]))
			sanitized = "_" + sanitized;

		return sanitized;
	}
}