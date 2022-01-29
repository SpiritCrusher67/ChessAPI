using ChessLibrary.Boards;

namespace ChessAPI.Infrastructure
{
    public class BoardsService
    {
        protected Dictionary<string,Board> _allActiveRooms = new Dictionary<string,Board>();

        public Board this[string roomId] => _allActiveRooms[roomId];

        public void AddRoom(string roomId, string ownerLogin, Board board)
        {
            _allActiveRooms[roomId] = board;
            NewRoomCreateEvent?.Invoke(roomId, ownerLogin);
        }

        /// <summary>
        /// Invokes when new room has created. First parameter - roomId. Second - ownerLogin
        /// </summary>
        public event Action<string, string> NewRoomCreateEvent;
    }
}
