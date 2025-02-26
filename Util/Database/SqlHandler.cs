using MySqlConnector;
using System.Collections;
using System.Data;
using System.Net;
using System.Text.Json;

namespace Script.Util.Database
{
    public class SqlHandler : IDisposable
    {
        private readonly MySqlConnection _connection;

        private static string DataTableSystemTextJson(DataTable dataTable)
        {
            if (dataTable.Rows.Count == 0) return "[]";

            IEnumerable<Dictionary<string, object?>> data = dataTable.Rows.OfType<DataRow>()
                .Select(row => dataTable.Columns.OfType<DataColumn>()
                    .ToDictionary(
                        col => col.ColumnName,
                        c =>
                        {
                            object value = row[c];
                            if (value == DBNull.Value)
                                return null;
                            return value is IDictionary { Count: 0 } ? null : value;
                        }
                    )
                );

            return JsonSerializer.Serialize(data);
        }

        public string Query(string query, Dictionary<string, object?>? sqlParams = null)
        {
            using MySqlCommand command = _connection.CreateCommand();
            command.CommandText = query;

            if (sqlParams != null)
                foreach (KeyValuePair<string, object?> sqlParam in sqlParams)
                    command.Parameters.AddWithValue(sqlParam.Key, sqlParam.Value ?? DBNull.Value);

            if (!query.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) && !query.Contains("RETURNING", StringComparison.OrdinalIgnoreCase))
            {
                int rowsAffected = command.ExecuteNonQuery();
                return rowsAffected.ToString();
            }

            DataTable dataTable = new();
            dataTable.Load(command.ExecuteReader());
            return DataTableSystemTextJson(dataTable);
        }


        public SqlHandler(string username, string password, string database, IPAddress address)
        {
            MySqlConnectionStringBuilder builder = new()
            {
                Server = address.ToString(),
                UserID = username,
                Password = password,
                Database = database,
                ConvertZeroDateTime = true
            };

            _connection = new MySqlConnection(builder.ConnectionString);
            _connection.Open();
        }

        public void Dispose()
        {
            _connection.Close();
            _connection.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
