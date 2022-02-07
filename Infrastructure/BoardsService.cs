using ChessAPI.Hubs;
using ChessLibrary.Main_units;
using Microsoft.AspNetCore.SignalR;

namespace ChessAPI.Infrastructure
{
    public class BoardsService
    {
        protected Dictionary<string,Board> _allActiveRooms = new Dictionary<string,Board>();
        IHubContext<ChessHub> _hubContext;

        public Board this[string roomId] => _allActiveRooms[roomId];

        public BoardsService(IHubContext<ChessHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public void AddRoom(string roomId, string ownerLogin, Board board)
        {
            _allActiveRooms[roomId] = board;
            NewRoomCreateEvent?.Invoke(roomId, ownerLogin);

            board.CheckHasSetedEvent += async (from, to) => await _hubContext.Clients.Group(roomId).SendAsync("ReceiveMessage", $"SERVER: {from.CurrentSide} side set CHECK to {to.CurrentSide}.");
            board.CheckMateHasSetedEvent += async (winner) => await _hubContext.Clients.Group(roomId).SendAsync("ReceiveMessage", $"SERVER: {winner.CurrentSide} set CHECK MATE! Match has ended.");
            board.MoveHasMakedEvent += async (from, to) => await _hubContext.Clients.Group(roomId).SendAsync("ReceiveMessage", $"SERVER: {to.FigureSide.ToString()} side make move from (y:{from.Y},x:{from.X}) to (y:{to.Y},x:{to.X})");
        }

        /// <summary>
        /// Invokes when new room has created. First parameter - roomId. Second - ownerLogin
        /// </summary>
        public event Action<string, string> NewRoomCreateEvent;
    }
}
