using Microsoft.AspNetCore.SignalR;
using ChessAPI.Infrastructure;
using ChessLibrary.Main_units;
using ChessLibrary.Takeing_Behaviors;
using ChessLibrary.Factories;
using Microsoft.AspNetCore.Authorization;

namespace ChessAPI.Hubs
{
    [Authorize]
    public class ChessHub : Hub
    {
        BoardsService _boardsService;
        ILogger<ChessHub> _logger;
        public ChessHub(BoardsService boardsService, ILogger<ChessHub> logger)
        {
            _boardsService = boardsService;
            _logger = logger;
        }

        public async Task SendMessage(string message, string gameId)
        {
            var userLogin = Context.User!.Identity!.Name!;
            var users = _boardsService.GetPlayers(gameId);

            await Clients.Users(users).SendAsync("ReceiveMessage", message, userLogin);
        }

        public async Task CreateGame()
        {
            var takeing = new DefaultTakeingBehavior();
            var factory = new DefaultFiguresFactory();
            var board = new DefaultBoard(takeing, factory);
            
            var gameId = Guid.NewGuid().ToString().Substring(0, 6);
            var userLogin = Context.User!.Identity!.Name!;

            _boardsService.AddGame(gameId, userLogin, board);
            
            await Clients.Caller.SendAsync("ReceiveMessage", $"SERVER: You create new game. Game id: {gameId}");
            await Clients.Caller.SendAsync("ReceiveCreatedGameId",gameId);

            var gamesData = await _boardsService.GetWaitingGamesDataList();

            await Clients.All.SendAsync("ReceiveActiveGames", gamesData);
        }
    

        public async Task JoinGame(string gameId)
        {
            var userLogin = Context.User!.Identity!.Name!;

            _boardsService.AddBlackSide(gameId, userLogin);

            var whiteSideUser = _boardsService.GetWhiteSideUserLogin(gameId);

            await Clients.Users(whiteSideUser,userLogin).SendAsync("ReceiveMessage", $"SERVER: {userLogin} has enterd the game. Match is starting!");

            await DisplayBoard(gameId);
            await Clients.User(whiteSideUser).SendAsync("SetTurn", true);
            await Clients.User(userLogin).SendAsync("SetTurn", false);

        }

        public async Task GetAvaliableFields(string gameId, int y, int x)
        {
            var userLogin = Context.User!.Identity!.Name!;
            if (userLogin == _boardsService.GetCurrentTurnuserLogin(gameId))
            {
                var fields = _boardsService[gameId].GetAvaliableFields((y, x));
                if (fields != null)
                    foreach (var field in fields)
                        await Clients.Caller.SendAsync("SetSelected", field.Y, field.X);
            }
        }

        private async Task DisplayBoard(string gameId, IEnumerable<Field> fields)
        {
            var users = _boardsService.GetPlayers(gameId);

            foreach (var field in fields)
                await Clients.Users(users).SendAsync("UpdateField", field.Y, field.X, field.Figure?.Name, (field.IsEmpty) ? "" : field.FigureSide.ToString());
        }
        public async Task DisplayBoard(string gameId) => await DisplayBoard(gameId, _boardsService[gameId].GetFields().Cast<Field>());
        
        public async Task MakeMove(string gameId, int yFrom, int xFrom, int yTo, int xTo)
        {
            var userLogin = Context.User!.Identity!.Name!;
            if (userLogin == _boardsService.GetCurrentTurnuserLogin(gameId))
            {
                var board = _boardsService[gameId];
                var fields = board.GetFields();
                var from = fields[yFrom, xFrom];
                var to = fields[yTo, xTo];
                var users = _boardsService.GetPlayers(gameId);
                if (board.MakeMove((yFrom, xFrom), (yTo, xTo)))
                {
                    await Clients.Users(users).SendAsync("UpdateField", from.Y, from.X, "", "");
                    await Clients.Users(users).SendAsync("UpdateField", to.Y, to.X, to.Figure!.Name, to.FigureSide.ToString());
                }
            }
        }
        public async Task GetActiveGames()
        {
            var gamesData = await _boardsService.GetWaitingGamesDataList();

            await Clients.Caller.SendAsync("ReceiveActiveGames", gamesData);
        }
        public override async Task OnConnectedAsync()
        {
            await GetActiveGames();

            await base.OnConnectedAsync();
        }
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await _boardsService.EndUserGames(Context.User!.Identity!.Name!);

            await base.OnDisconnectedAsync(exception);
        }
    }
}
