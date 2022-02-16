namespace ChessAPI.Infrastructure
{
    public interface IDBSqlExecuter
    {
        Task<string> GetJsonResult(string sqlQuery, Dictionary<string, object>? parameters = null);

        Task<int> ExecuteQuery(string sqlQuery, Dictionary<string, object>? parameters = null);
    }
}
