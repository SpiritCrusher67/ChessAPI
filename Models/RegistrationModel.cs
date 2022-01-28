using System.ComponentModel.DataAnnotations;

namespace ChessAPI.Models
{
    public class RegistrationModel
    {
        [Required]
        public string Login { get; set; }
        [Required]
        public string Name { get; set; }
        [Required]
        [MinLength(5)]
        public string Password { get; set; }

        [Compare("Password", ErrorMessage = "Passwords mismatch")]
        public string ConfirmPassword { get; set; }

    }
}
