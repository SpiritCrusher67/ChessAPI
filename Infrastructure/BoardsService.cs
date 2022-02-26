using ChessAPI.Hubs;
using ChessAPI.Models;
using ChessLibrary.Main_units;
using Microsoft.AspNetCore.SignalR;

namespace ChessAPI.Infrastructure
{
    public class BoardsService
    {
        protected Dictionary<string, Board> _allActiveGames = new();
        protected Dictionary<string, GamePlayersModel> _allGamePlayers = new();
        IHubContext<ChessHub> _hubContext;
        IDBSqlExecuter _dBSqlExecuter;

        public Board this[string roomId] => _allActiveGames[roomId];

        public BoardsService(IDBSqlExecuter dBSqlExecuter, IHubContext<ChessHub> hubContext)
        {
            _hubContext = hubContext;
            _dBSqlExecuter = dBSqlExecuter;
        }

        public void AddBlackSide(string gameId, string userLogin)
        {
            _allGamePlayers[gameId].BlackSideUserLogin = userLogin;
            AddEventsListeners(gameId);
        }

        public string? GetCurrentTurnuserLogin(string gameId)
        {
            var board = this[gameId];

            return (board.CurrentTurnSide.CurrentSide == SideEnum.White) ? GetWhiteSideUserLogin(gameId) : GetBlackSideUserLogin(gameId);
        }

        private void AddEventsListeners(string gameId)
        {
            var board = _allActiveGames[gameId];
            var users = GetPlayers(gameId);

            board.CheckHasSetedEvent += async (from, to) => await _hubContext.Clients.Users(users).SendAsync("ReceiveMessage", $"{from.CurrentSide} side set CHECK to {to.CurrentSide}.");
            board.CheckMateHasSetedEvent += async (loseSide) =>
                await EndGame(gameId, (loseSide.CurrentSide == SideEnum.White) ? GetWhiteSideUserLogin(gameId) : GetBlackSideUserLogin(gameId));

            board.MoveHasMakedEvent += async (from, to) =>
            {
                await _hubContext.Clients.Users(users).SendAsync("ReceiveMessage", $"{to.FigureSide.ToString()} side make move from (y:{from.Y},x:{from.X}) to (y:{to.Y},x:{to.X})");
                var blackUser = GetBlackSideUserLogin(gameId);
                var whiteUser = GetWhiteSideUserLogin(gameId);
                await _hubContext.Clients.User(blackUser).SendAsync("SetTurn", to.FigureSide != SideEnum.Black);
                await _hubContext.Clients.User(whiteUser).SendAsync("SetTurn", to.FigureSide != SideEnum.White);
            };
        }

        public async Task EndGame(string gameId, string loseUserLogin)
        {
            if (_allActiveGames.Remove(gameId))
            {
                var users = GetPlayers(gameId);
                if (GetWhiteSideUserLogin(gameId) != GetBlackSideUserLogin(gameId))
                {
                    var winLogin = users.First(u => u != loseUserLogin);
                    var loseLogin = users.First(u => u == loseUserLogin);

                    var query = "INSERT INTO GamesResults VALUES(@winLogin, @loseLogin, DEFAULT)";
                    var parameters = new Dictionary<string, object>
                    {
                        { "@winLogin", winLogin },
                        { "@loseLogin", loseLogin }
                    };

                    await _dBSqlExecuter.ExecuteQuery(query, parameters);
                }

                foreach (var user in users)
                    await _hubContext.Clients.User(user).SendAsync("EndGame", user != loseUserLogin);

                _allGamePlayers.Remove(gameId);
            }
        }
        public async Task EndUserGames(string userLogin)
        {
            var games = _allGamePlayers
                .Where(pair => pair.Value.WhiteSideUserLogin == userLogin || pair.Value.BlackSideUserLogin == userLogin)
                .Select(pair => pair.Key);

            foreach (var game in games)
                await EndGame(game, userLogin);
        }

        public IEnumerable<string> GetPlayers(string gameId) => new List<string>() { _allGamePlayers[gameId].BlackSideUserLogin, _allGamePlayers[gameId].WhiteSideUserLogin };
        public string? GetWhiteSideUserLogin(string gameId) => _allGamePlayers.FirstOrDefault(p => p.Key == gameId).Value?.WhiteSideUserLogin;
        public string? GetBlackSideUserLogin(string gameId) => _allGamePlayers.FirstOrDefault(p => p.Key == gameId).Value?.BlackSideUserLogin;

        public void AddGame(string gameId, string ownerLogin, Board board)
        {
            _allActiveGames[gameId] = board;
            _allGamePlayers.Add(gameId, new() { WhiteSideUserLogin = ownerLogin });
            NewRoomCreateEvent?.Invoke(gameId, ownerLogin);

        }

        public async Task<IEnumerable<GameDataModel>> GetWaitingGamesDataList()
        {
            var gamesData = _allGamePlayers
            .Where(p => string.IsNullOrEmpty(p.Value.BlackSideUserLogin))
            .Select(p => new GameDataModel { GameId = p.Key, CreatorLogin = p.Value.WhiteSideUserLogin });
            var result = new List<GameDataModel>();
            foreach (var gameData in gamesData)
            {
                var query = "SELECT Name FROM Users WHERE Login = @login";
                var parameters = new Dictionary<string, object> 
                {
                    { "@login", gameData.CreatorLogin } 
                };
                gameData.CreatorName = (await _dBSqlExecuter
                    .GetJsonResult(query, parameters))
                    .First()["Name"].ToString()!;
                result.Add(gameData);
            }
            return result;
        } 

        /// <summary>
        /// Invokes when new room has created. First parameter - gameId. Second - ownerLogin
        /// </summary>
        public event Action<string, string> NewRoomCreateEvent;
    }
}
