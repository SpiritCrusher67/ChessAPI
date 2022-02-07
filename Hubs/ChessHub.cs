using Microsoft.AspNetCore.SignalR;
using ChessAPI.Infrastructure;
using ChessLibrary.Main_units;
using ChessLibrary.Takeing_Behaviors;
using ChessLibrary.Factories;
using Microsoft.AspNetCore.Authorization;

namespace ChessAPI.Hubs
{
    //[Authorize]
    public class ChessHub : Hub
    {
        BoardsService _boardsService;
        ILogger<ChessHub> _logger;
        public ChessHub(BoardsService boardsService, ILogger<ChessHub> logger)
        {
            _boardsService = boardsService;
            _logger = logger;
        }

        public async Task SendMessageToRoom(string message, string roomId)
        {
            await Clients.Group(roomId).SendAsync("ReceiveMessage", $"{Context.UserIdentifier ?? Context.ConnectionId}: {message}");
        }

        public async Task CreateRoom()
        {
            var takeing = new DefaultTakeingBehavior();
            var factory = new DefaultFiguresFactory();
            var board = new DefaultBoard(takeing, factory);
            
            var roomId = Guid.NewGuid().ToString().Substring(0, 6);
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

            _boardsService.AddRoom(roomId, Context.UserIdentifier!, board);

            await Clients.Caller.SendAsync("ReceiveMessage", $"SERVER: You create new room. Room id: {roomId}");
        }

        public async Task JoinRoom(string roomId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

            await Clients.Group(roomId).SendAsync("ReceiveMessage", $"SERVER: {Context.UserIdentifier ?? Context.ConnectionId} has enterd the room. Match is starting!");

            await DisplayBoard(roomId);

            await Clients.GroupExcept(roomId, Context.ConnectionId).SendAsync("ChangeTurn");
        }

        public async Task GetAvaliableFields(string roomId, int y, int x)
        {
            var fields = _boardsService[roomId].GetAvaliableFields((y, x));
            if (fields != null)
                foreach (var field in fields)
                    await Clients.Caller.SendAsync("SetSelected", field.Y, field.X);
            
        }

        private async Task DisplayBoard(string roomId, IEnumerable<Field> fields)
        {
            foreach (var field in fields)
            {
                await Clients.Group(roomId).SendAsync("UpdateBoard", field.Y, field.X, field.Figure?.Name, (field.IsEmpty) ? "" : field.FigureSide.ToString());
            }
        }
        public async Task DisplayBoard(string roomId) => await DisplayBoard(roomId, _boardsService[roomId].GetFields().Cast<Field>());
        
        public async Task MakeMove(string roomId, int yFrom, int xFrom, int yTo, int xTo)
        {
            var board = _boardsService[roomId];
            var fields = board.GetFields();
            var from = fields[yFrom, xFrom];
            var to = fields[yTo, xTo];
            if (board.MakeMove((yFrom,xFrom), (yTo,xTo)))
            {
                await Clients.Group(roomId).SendAsync("UpdateBoard", from.Y, from.X, from.Figure?.Name, (from.IsEmpty) ? "" : from.FigureSide.ToString());
                await Clients.Group(roomId).SendAsync("UpdateBoard", to.Y, to.X, to.Figure?.Name, (to.IsEmpty) ? "" : to.FigureSide.ToString());

                await Clients.Group(roomId).SendAsync("ChangeTurn");
            }
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Someone disconnected wich " + exception?.Message);
            return base.OnDisconnectedAsync(exception);
        }
    }
}
