using System.Data.SqlClient;
using System.Text;

namespace ChessAPI.Infrastructure
{
    public class DefaultDBSqlExecuter : IDBSqlExecuter
    {
        string connectionString;
        public DefaultDBSqlExecuter(IConfiguration configuration)
        {
            connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public async Task<string> GetJsonResult(string sqlQuery, Dictionary<string, object>? parameters = null)
        {
            var resultList = new List<string>();

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                var command = new SqlCommand(sqlQuery, connection);
                if (parameters?.Count > 0)
                    AddParamsToCommand(command, parameters);

                var reader = await command.ExecuteReaderAsync();
                if (reader.HasRows)
                {

                    await ReadDataToListAsync(reader, resultList);
                }

                await reader.CloseAsync();
            }

            return ConvertListToJson(resultList);
        }

        public async Task<int> ExecuteQuery(string sqlQuery, Dictionary<string, object>? parameters = null)
        {
            using var connection = new SqlConnection(connectionString);

            await connection.OpenAsync();

            var command = new SqlCommand(sqlQuery, connection);
            if (parameters?.Count > 0)
                AddParamsToCommand(command, parameters);

            return await command.ExecuteNonQueryAsync();
        }

        #region Utility methods
        private void AddParamsToCommand(SqlCommand command, Dictionary<string, object> parameters)
        {
            foreach (var param in parameters)
                command.Parameters.Add(new SqlParameter(param.Key, param.Value));
        }

        private async Task ReadDataToListAsync(SqlDataReader reader,List<string> list)
        {
            var columns = GetColumnsFromReader(reader);

            while (await reader.ReadAsync())
            {
                var item = new StringBuilder();

                foreach (var column in columns)
                {
                    var value = reader[column]?.ToString()?.Trim();
                    item.Append(columns.Count > 1 ? $"\"{column}\": \"{value}\", " : value);
                }

                list.Add(columns.Count > 1 ? $"{{ {item} }}" : item.ToString());
            }
        }

        private List<string> GetColumnsFromReader(SqlDataReader reader)
        {
            var result = new List<string>();

            for (int i = 0; i < reader.FieldCount; i++)
                result.Add(reader.GetName(i));

            return result;
        }

        private string ConvertListToJson(List<string> list)
        {
            if (list.Count == 1)
                return list.First();

            if (list.Count > 0)
            {
                var result = new StringBuilder("[ ");
                foreach (var item in list)
                    result.Append(item + ", ");

                return result.Insert(result.Length, "]").ToString();
            }
            return string.Empty;
        }
        #endregion

    }
}
