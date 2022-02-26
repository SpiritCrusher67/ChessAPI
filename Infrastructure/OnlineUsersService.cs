namespace ChessAPI.Infrastructure
{
    public class OnlineUsersService
    {
        List<string> _connectedUsers = new();
        IDBSqlExecuter _dBSqlExecuter;

        public OnlineUsersService(IDBSqlExecuter dBSqlExecuter)
        {
            _dBSqlExecuter = dBSqlExecuter;
        }
        public bool IsUserOnline(string userLogin)
        {
            return _connectedUsers.Contains(userLogin);
        }

        public void AddUserToOnlineList(string userLogin)
        {
            _connectedUsers.Add(userLogin);
        }
        public void RemoveUserFromOnlineList(string userLogin)
        {
            _connectedUsers.Remove(userLogin);
        }

        public async Task<IEnumerable<Dictionary<string,object>>> GetOnlineFriends(string userLogin)
        {
            var query = @"SELECT Login, Name FROM Users 
                        JOIN Friends ON Friends.FriendLogin = Users.Login
                        WHERE Friends.UserLogin = @login";
            var parameters = new Dictionary<string, object>
            {
                { "@login", userLogin! }
            };

            var friendsData = (await _dBSqlExecuter.GetJsonResult(query, parameters))
                .Where(f => _connectedUsers.Contains(f["Login"]))
                .ToList();

            return friendsData;
        }
    }
}
