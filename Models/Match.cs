
namespace ChessAPI.Models
{
    public class Match
    {
        public string Id { get; set; }
        
        public int OwnderId { get; set; }

        //public User Owner { get; set; }
        public int? OpponentId { get; set; }

        //public User? Oponnent { get; set; }
        
        public string SecuenceOfMoves { get; set; }
    }
}
