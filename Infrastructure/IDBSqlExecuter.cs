namespace ChessAPI.Infrastructure
{
    public interface IDBSqlExecuter
    {
        Task<string> GetJsonResult(string sqlQuery, Dictionary<string, object>? parameters = null);

        Task<int> ExecuteQuery(string sqlQuery, Dictionary<string, object>? parameters = null);

        Task<(string, int)> ExecuteQueryOutIntParameter(string sqlQuery, string outParamName, Dictionary<string, object>? inParams = null);
    }
}
