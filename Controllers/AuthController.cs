using ChessAPI.Infrastructure;
using ChessAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChessAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        IDBSqlExecuter _dBSqlExecuter;
        ITokenService _tokenService;
        IConfiguration _configuration;
        public AuthController(IDBSqlExecuter dBSqlExecuter, ITokenService tokenService, IConfiguration configuration)
        {
            _dBSqlExecuter = dBSqlExecuter;
            _tokenService = tokenService;
            _configuration = configuration;
        }

        [HttpPost, Route("Login")]
        public async Task<ActionResult> LoginAsync([FromForm] LoginModel loginModel)
        {
            if (loginModel == null)
                return BadRequest("Invalid client request");
            

            var query = "EXEC @result = TryAuthorize @login, @password";
            var parameters = new Dictionary<string, object>
            {
                { "@login", loginModel.Login },
                { "@password", loginModel.Password}
            };

            if ((await _dBSqlExecuter.ExecuteQueryOutIntParameter(query, "@result", parameters)).Item2 != 1)
                return Unauthorized();

            var role = await GetUserRoleAsync(loginModel.Login);
            if (role == null)
                return BadRequest("Invalid client request");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, loginModel.Login),
                new Claim(ClaimTypes.Role, role)
            };
            var accessToken = _tokenService.GenerateAccessToken(claims);
            var refreshToken = _tokenService.GenerateRefreshToken();

            query = "EXEC AddRefreshToken @login, @token, @expiryTime";
            parameters = new Dictionary<string, object>
            {
                { "@login", loginModel.Login },
                { "@token", refreshToken},
                { "@expiryTime", DateTime.Now.AddDays(double.Parse(_configuration["TokenData:RefreshTokenExpiresDays"]))}
            };

            await _dBSqlExecuter.ExecuteQuery(query, parameters);

            return Ok(new
            {
                Login = loginModel.Login,
                Token = accessToken,
                RefreshToken = refreshToken
            });
        }

        [HttpPost]
        [Route("Token/Refresh")]
        public async Task<ActionResult> RefreshTokenAsync([FromForm]TokenModel tokenModel)
        {
            if (tokenModel == null)
                return BadRequest("Invalid client request");
            
            var accessToken = tokenModel.AccessToken;
            var refreshToken = tokenModel.RefreshToken;

            var principal = _tokenService.GetPrincipalFromExpiredToken(accessToken);
            var login = principal.Identity?.Name;

            if (login == null)
                return BadRequest("Invalid client request");

            var result = await _dBSqlExecuter.GetJsonResult("EXEC GetRefreshToken @login", new() { { "@login", login } });
            var userRefreshToken = result.FirstOrDefault()?["RefreshToken"].ToString();
            DateTime.TryParse(result.FirstOrDefault()?["RefreshTokenExpiryTime"].ToString(), out var userRefreshTokenExpiryTime);

            if ( userRefreshToken != refreshToken || userRefreshTokenExpiryTime <= DateTime.Now)
                return BadRequest("Invalid client request");
            
            var newAccessToken = _tokenService.GenerateAccessToken(principal.Claims);
            var newRefreshToken = _tokenService.GenerateRefreshToken();

            await _dBSqlExecuter.ExecuteQuery("EXEC AddRefreshToken @login, @token, @expiryTime", new()
            {
                { "@login", login },
                { "@token", refreshToken },
                { "@expiryTime", userRefreshTokenExpiryTime }
            });

            return new ObjectResult(new
            {
                Login = login,
                Token = newAccessToken,
                RefreshToken = newRefreshToken
            });
        }

        [HttpPost, Authorize]
        [Route("Token/Revoke")]
        public async Task<IActionResult> RevokeAsync()
        {
            var login = User.Identity!.Name!;

            await _dBSqlExecuter.ExecuteQuery("EXEC RevokeRefreshToken @login", new()
            {
                { "@login", login }
            });

            return NoContent();
        }

        private async Task<string?> GetUserRoleAsync(string login)
        {
            var query = "SELECT Role FROM Users WHERE Login = @login";
            var result = await _dBSqlExecuter.GetJsonResult(query, new() { { "@login", login } });
            return (result.FirstOrDefault()?["Role"]?.ToString());
        }
    }
}
