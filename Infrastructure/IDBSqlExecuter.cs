namespace ChessAPI.Infrastructure
{
    public interface IDBSqlExecuter
    {
        Task<IEnumerable<Dictionary<string, object>>> GetJsonResult(string sqlQuery, Dictionary<string, object>? parameters = null);

        Task<int> ExecuteQuery(string sqlQuery, Dictionary<string, object>? parameters = null);

        Task<(IEnumerable<Dictionary<string, object>>, int)> ExecuteQueryOutIntParameter(string sqlQuery, string outParamName, Dictionary<string, object>? inParams = null);
    }
}
