using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace ChessAPI.Infrastructure
{
    public static class AuthOptions
    {
        const string KEY = "mysupersecret_secretkey!123";
        public const int LIFETIME = 1;
        public static SymmetricSecurityKey GetSymmetricSecurityKey()
        {
            return new SymmetricSecurityKey(Encoding.ASCII.GetBytes(KEY));
        }
    }
}
