using Microsoft.AspNetCore.SignalR;
using ChessAPI.Infrastructure;
using ChessLibrary.Boards;
using Microsoft.AspNetCore.Authorization;

namespace ChessAPI.Hubs
{
    [Authorize]
    public class ChessHub : Hub
    {
        BoardsService _boardsService;
        public ChessHub(BoardsService boardsService)
        {
            _boardsService = boardsService;
        }

        public async Task CreateRoom()
        {
            var board = new Board();
            board.InitializeBoard();
            
            board.CheckHasSeted += async (from, to) => await Clients.Caller.SendAsync("ReceiveMessage", $"{from.CurrentSide} side set CHECK to {to.CurrentSide}.");
            board.CheckMateHasSeted += async (winner) => await Clients.Caller.SendAsync("ReceiveMessage", $"{winner.CurrentSide} set CHECK MATE! Match has ended.");

            var roomId = Guid.NewGuid().ToString().Substring(0, 6);
            await Groups.AddToGroupAsync(Context.UserIdentifier!, roomId);

            _boardsService.AddRoom(roomId, Context.UserIdentifier!, board);

            await Clients.Caller.SendAsync("ReceiveMessage", roomId);
        }

        public async Task JoinRoom(string roomId)
        {
            var fields = _boardsService[roomId].GetFields();

            await Groups.AddToGroupAsync(Context.UserIdentifier!, roomId);

            await Clients.Caller.SendAsync("ReceiveMessage", $"{Context.User!.Identity!.Name} has enterd the room. Match is starting!");

            await Clients.Group(roomId).SendAsync("UpdateBoard", fields);

            await Clients.GroupExcept(roomId, Context.ConnectionId).SendAsync("MyTurn");
        }

        public async Task<bool> MakeMove(string roomId, (int, int) from, (int, int) to)
        {
            var board = _boardsService[roomId];
            if (board.MakeMove(from, to))
            {
                await Clients.Group(roomId).SendAsync("UpdateBoard", board.GetFields());
                await Clients.GroupExcept(roomId, Context.ConnectionId).SendAsync("MyTurn");

                return true;
            }
            return false;
        }
    }
}
