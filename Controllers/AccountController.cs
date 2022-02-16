using ChessAPI.Infrastructure;
using ChessAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ChessAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        IDBSqlExecuter _dBSqlExecuter;
        public AccountController(IDBSqlExecuter dBSqlExecuter)
        {
            _dBSqlExecuter = dBSqlExecuter;
        }

        #region Public actions

        [HttpPost("/token")]
        public async Task<ActionResult> Token(string login, string password)
        {
            var identity = await GetIdentity(login, password);

            if (identity == null)
                return BadRequest(new { errorText = "Invalid login or password." });

            var now = DateTime.UtcNow;

            var jwt = new JwtSecurityToken(
                    notBefore: now,
                    claims: identity.Claims,
                    expires: now.Add(TimeSpan.FromMinutes(AuthOptions.LIFETIME)),
                    signingCredentials: new SigningCredentials(AuthOptions.GetSymmetricSecurityKey(), SecurityAlgorithms.HmacSha256));
            var encodedJwt = new JwtSecurityTokenHandler().WriteToken(jwt);

            return Ok(new TokenModel(encodedJwt, login));
        }

        [HttpPost]
        public async Task<ActionResult> CreateAccount(RegistrationModel registrationData)
        {
            if (!await CheLoginAvailability(registrationData.Login))
                ModelState.AddModelError("Login", "Login is already taken");

            if (ModelState.IsValid)
            {
                var query = "INSERT INTO Users VALUES (@login, @password, @name, @role)";
                var parameters = new Dictionary<string, object>
            {
                { "@login", registrationData.Login },
                { "@password", registrationData.Password },
                { "@name", registrationData.Name },
                { "@role", "User" }
            };

                if (await _dBSqlExecuter.ExecuteQuery(query, parameters) == 1)
                    return Ok("Account has been create. You can get token.");

                return BadRequest();
            }

            return ValidationProblem();
        }

        #endregion

        #region Authorize actions
        [HttpGet]
        [Authorize]
        public async Task<ActionResult> Get()
        {
            var user = User.Identity?.Name;
            var query = "SELECT * FROM Users WHERE Login = @login";
            var parameters = new Dictionary<string, object>();
            parameters.Add("@login", user!);

            var result = await _dBSqlExecuter.GetJsonResult(query, parameters);
            return Ok(result);
        }

        [HttpPut]
        [Route("/ChangePassword")]
        [Authorize]
        public async Task<ActionResult> ChangePassword(string oldPassword, string newPassword)
        {
            var login = User.Identity?.Name;

            if (await GetUserRoleByAuthData(login!, oldPassword) == string.Empty)
                return BadRequest(new { errorText = "Invalid old password." });

            var query = "UPDATE Users SET Password = @password WHERE Login = @login";
            var parameters = new Dictionary<string, object>
            {
                { "@login", login! },
                { "@password", newPassword }
            };

            if (await _dBSqlExecuter.ExecuteQuery(query, parameters) == 1)
                return Ok();

            return BadRequest();
        }

        [HttpDelete]
        [Authorize]
        public async Task<ActionResult> DeleteUserAsync(string password)
        {
            var login = User.Identity?.Name;

            if (await GetUserRoleByAuthData(login!, password) == string.Empty)
                return BadRequest(new { errorText = "Invalid password." });

            var query = "DELETE FROM Users WHERE Login = @login";
            var parameters = new Dictionary<string, object>
            {
                { "@login", login! }
            };

            //TODO: Logout if user deleted
            if (await _dBSqlExecuter.ExecuteQuery(query, parameters) == 1)
                return Ok();

            return BadRequest();
        }
        #endregion

        #region Admin actions

        [HttpDelete]
        [Route("/Delete/{login}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> DeleteUser(string login)
        {
            var query = "DELETE FROM Users WHERE Login = @login";
            var parameters = new Dictionary<string, object>
            {
                { "@login", login! }
            };

            if (await _dBSqlExecuter.ExecuteQuery(query, parameters) == 1)
                return Ok();

            return BadRequest(new { errorText = "User not found." });
        }

        [HttpGet]
        [Route("/UserData/{login}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> GetUserDataAsync(string login)
        {
            var query = "SELECT * FROM Users WHERE Login = @login";
            var parameters = new Dictionary<string, object>
            {
                { "@login", login! }
            };

            var result = await _dBSqlExecuter.GetJsonResult(query, parameters);

            if (result == string.Empty)
                return BadRequest(new { errorText = "User not found." });

            return Ok(result);
        }

        [HttpGet]
        [Route("/UserData")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> GetUsersDataAsync()
        {
            var query = "SELECT * FROM Users";

            var result = await _dBSqlExecuter.GetJsonResult(query);

            return Ok(result);
        }
        #endregion

        #region Utility methods
        private async Task<ClaimsIdentity?> GetIdentity(string login, string password)
        {
            var userRole = await GetUserRoleByAuthData(login, password);

            if (userRole == string.Empty)
                return null;

            var claims = new List<Claim>
            {
                new Claim(ClaimsIdentity.DefaultNameClaimType, login),
                new Claim(ClaimsIdentity.DefaultRoleClaimType, userRole)
            };
            ClaimsIdentity claimsIdentity =
            new ClaimsIdentity(claims, "Token", ClaimsIdentity.DefaultNameClaimType,
                ClaimsIdentity.DefaultRoleClaimType);

            return claimsIdentity;
        }

        private async Task<string> GetUserRoleByAuthData(string login, string password)
        {
            var query = "SELECT Role FROM Users WHERE Login = @login AND Password = @password";
            var parameters = new Dictionary<string, object>();
            parameters.Add("@login", login);
            parameters.Add("@password", password);

            return await _dBSqlExecuter.GetJsonResult(query, parameters);
        }

        private async Task<bool> CheLoginAvailability(string login)
        {
            var query = "SELECT Login FROM Users WHERE Login = @login";
            var parameters = new Dictionary<string, object>();
            parameters.Add("@login", login);

            return await _dBSqlExecuter.GetJsonResult(query, parameters) == string.Empty;
        }
        #endregion
    }
}

