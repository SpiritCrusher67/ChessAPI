using System.ComponentModel.DataAnnotations;

namespace ChessAPI.Models
{
    public class PostModel
    {
        [Required]
        [MaxLength(50)]
        public string Title { get; set; }
        [Required]
        [MaxLength(600)]
        public string Text { get; set; }
        public string Tags { get; set; }
        public IFormFile? PostImage { get; set; }

    }
}
