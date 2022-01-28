using Microsoft.EntityFrameworkCore;

namespace ChessAPI.Models
{
    [Owned]
    public class UserData
    {
        public string Login { get; set; }
        public string Password { get; set; }
        public string Role { get; set; }
    }
}
