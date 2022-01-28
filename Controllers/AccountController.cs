using ChessAPI.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ChessAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        AppdbContext _dbContext;
        public AccountController(AppdbContext dbContext)
        {
            _dbContext = dbContext;
        }
        [HttpPost("/token")]
        public ActionResult<(string,string)> Token(string login, string password)
        {
            var identity = GetIdentity(login, password);
            if (identity == null)
            {
                return BadRequest(new { errorText = "Invalid login or password." });
            }

            var now = DateTime.UtcNow;
            // создаем JWT-токен
            var jwt = new JwtSecurityToken(
                    notBefore: now,
                    claims: identity.Claims,
                    expires: now.Add(TimeSpan.FromMinutes(AuthOptions.LIFETIME)),
                    signingCredentials: new SigningCredentials(AuthOptions.GetSymmetricSecurityKey(), SecurityAlgorithms.HmacSha256));
            var encodedJwt = new JwtSecurityTokenHandler().WriteToken(jwt);

            return Ok((login,encodedJwt));
        }

        private ClaimsIdentity GetIdentity(string username, string password)
        {
            var user = _dbContext.Users.Include(u => u.Data).FirstOrDefault(x => x.Data.Login == username && x.Data.Password == password);
            if (user != null)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimsIdentity.DefaultNameClaimType, user.Data.Login),
                    new Claim(ClaimsIdentity.DefaultRoleClaimType, user.Data.Role)
                };
                ClaimsIdentity claimsIdentity =
                new ClaimsIdentity(claims, "Token", ClaimsIdentity.DefaultNameClaimType,
                    ClaimsIdentity.DefaultRoleClaimType);
                return claimsIdentity;
            }

            // если пользователь не найден
            return null;
        }
    }
}
