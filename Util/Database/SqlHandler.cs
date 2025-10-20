using MySqlConnector;
using System.Data;
using System.Globalization;
using System.Net;

namespace Script.Util.Database
{
	public class DatabaseService
	{
		private static readonly HashSet<string> NullStrings = new(StringComparer.OrdinalIgnoreCase)
		{
			"null", "nil", "nul"
		};

		private readonly string _connectionString;

		public DatabaseService(string username, string password, string database, IPAddress address, int port = 3306)
		{
			MySqlConnectionStringBuilder builder = new()
			{
				Server = address.ToString(),
				Port = (uint)port,
				UserID = username,
				Password = password,
				Database = database,
				ConvertZeroDateTime = true,
				Pooling = true,
				MinimumPoolSize = 5,
				MaximumPoolSize = 100,
				ConnectionTimeout = 15,
				DefaultCommandTimeout = 30,
				ConnectionLifeTime = 300
			};

			_connectionString = builder.ConnectionString;
		}

		private MySqlConnection GetConnection() => new(_connectionString);

		private static string DataTableToJson(DataTable dataTable, bool array)
		{
			if (dataTable.Rows.Count == 0) return "[]";

			List<Dictionary<string, object?>> list = dataTable.Rows.OfType<DataRow>()
				.Select(row => dataTable.Columns.OfType<DataColumn>()
				.ToDictionary(col => col.ColumnName, c => ConvertValue(row[c])))
				.ToList();

			if (!array && list.Count == 1)
				return JsonSerializer.Serialize(list[0]);

			return JsonSerializer.Serialize(list);
		}

		private static bool IsJsonObject(string str)
		{
			str = str.Trim();
			return str.StartsWith('{') && str.EndsWith('}');
		}

		private static bool IsJsonArray(string str)
		{
			str = str.Trim();
			return str.StartsWith('[') && str.EndsWith(']');
		}

		private static object? ConvertValue(object value)
		{
			return value switch
			{
				DBNull => null,
				byte[] bytes => Convert.ToBase64String(bytes),
				TimeSpan ts => ts.ToString(),
				long l => l.ToString(CultureInfo.InvariantCulture),
				decimal dec => dec.ToString(CultureInfo.InvariantCulture),
				string s when IsJsonObject(s) => JsonSerializer.Deserialize<Dictionary<string, object?>>(s),
				string s when IsJsonArray(s) => JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(s),
				"true" => true,
				"false" => false,
				_ => value
			};
		}
		
		public async Task<string> QueryJsonAsync(string query, Dictionary<string, object?>? sqlParams = null, bool array = true)
		{
			try
			{
				await using MySqlConnection connection = GetConnection();
				await connection.OpenAsync();

				await using MySqlCommand command = connection.CreateCommand();
				command.CommandText = query;

				if (sqlParams != null)
				{
					foreach ((string? key, object? value) in sqlParams)
					{
						switch (value)
						{
							case null:
							case string s when NullStrings.Contains(s):
								command.Parameters.AddWithValue(key, DBNull.Value);
								break;
							default:
								command.Parameters.AddWithValue(key, value);
								break;
						}
					}
				}

				string firstWord = query.TrimStart().Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToUpperInvariant() ?? "";

				bool isSelect = firstWord is "SELECT" or "SHOW" or "DESCRIBE" or "EXPLAIN" or "CALL";

				if (!isSelect)
				{
					int rowsAffected = await command.ExecuteNonQueryAsync();
					return JsonSerializer.Serialize(new { rowsAffected });
				}

				DataTable dataTable = new();
				await using MySqlDataReader reader = await command.ExecuteReaderAsync();
				dataTable.Load(reader);

				return DataTableToJson(dataTable, array);
			}
			catch (Exception e)
			{
				Debug.WriteLine(e.ToString());
				return JsonSerializer.Serialize(new { error = "Database query failed" });
			}
		}

		public async Task<bool> ExecuteTransactionAsync(Func<MySqlTransaction, MySqlConnection, Task> action)
		{
			await using MySqlConnection connection = GetConnection();
			await connection.OpenAsync();

			await using MySqlTransaction transaction = await connection.BeginTransactionAsync();
			try
			{
				await action(transaction, connection);
				await transaction.CommitAsync();
				return true;
			}
			catch (Exception e)
			{
				await transaction.RollbackAsync();
				return false;
			}
		}
	}
}
