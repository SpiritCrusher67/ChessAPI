using System.ComponentModel.DataAnnotations.Schema;

namespace ChessAPI.Models
{
    public class Match
    {
        public string Id { get; set; }
        
        public int OwnderId { get; set; }

        [ForeignKey("OwnderId")]
        public User Owner { get; set; }
        public int? OpponentId { get; set; }

        [ForeignKey("OpponentId")]
        public User? Oponnent { get; set; }
        
        public string SecuenceOfMoves { get; set; }
    }
}
