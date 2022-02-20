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
        IWebHostEnvironment _appEnvironment;
        public AccountController(IDBSqlExecuter dBSqlExecuter, IWebHostEnvironment appEnvironment)
        {
            _dBSqlExecuter = dBSqlExecuter;
            _appEnvironment = appEnvironment;
        }

        #region Public actions

        [HttpPost]
        [Route("token")]
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
        public async Task<ActionResult> CreateAccount([FromForm]RegistrationModel registrationData)
        {
            if (!await CheLoginAvailability(registrationData.Login))
                ModelState.AddModelError("Login", "Login is already taken");

            var t = registrationData.ProfileImage?.GetType();

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
                {
                    if (registrationData.ProfileImage != null)
                    {
                        var path = $"{_appEnvironment.WebRootPath}/img/UserImages/{registrationData.Login}";

                        Directory.CreateDirectory(path);

                        using (var fileStream = new FileStream(path + "/Profile.jpg", FileMode.Create))
                        {
                            await registrationData.ProfileImage.CopyToAsync(fileStream);
                        }
                    }

                    return Ok("Account has been create. You can get token.");
                }

                return BadRequest();
            }

            return ValidationProblem();
        }

        [HttpGet("GetName")]
        public async Task<ActionResult> GetUserName(string login)
        {
            var query = "SELECT Name From Users WHERE Login = @login";
            var parameters = new Dictionary<string, object>
            {
                { "@login", login }
            };

            var result = await _dBSqlExecuter.GetJsonResult(query, parameters);

            return Ok(result.FirstOrDefault()?["Name"]);

        }

        [HttpGet("GetProfileImage")]
        public async Task<ActionResult> GetProfileImage(string login)
        {
            var path = $"{_appEnvironment.WebRootPath}/img/UserImages/{login ?? User.Identity?.Name}/Profile.jpg";

            if (System.IO.File.Exists(path))
                return PhysicalFile(path,"image/jpeg");

            return PhysicalFile($"{_appEnvironment.WebRootPath}/img/UserImages/ProfileDefault.jpg", "image/jpeg");
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
        [Route("ChangePassword")]
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

        [HttpGet("Users/ByName")]
        [Authorize]
        public async Task<ActionResult> GetUsersByNameAsync(string name)
        {
            var query = $"SELECT * FROM Users WHERE Name LIKE '%{name}%'";

            return Ok(await _dBSqlExecuter.GetJsonResult(query));
        }

        [HttpGet]
        [Route("UserData/{login}")]
        [Authorize]
        public async Task<ActionResult> GetUserDataAsync(string login)
        {
            var query = "SELECT * FROM Users WHERE Login = @login";
            var parameters = new Dictionary<string, object>
            {
                { "@login", login! }
            };

            var result = await _dBSqlExecuter.GetJsonResult(query, parameters);

            if (result.Count() == 0)
                return BadRequest(new { errorText = "User not found." });

            return Ok(result);
        }

        #region Friend actions 

        [HttpGet("GetFriends")]
        [Authorize]
        public async Task<ActionResult> GetFriendsListAsync()
        {
            var query = "SELECT Friends.FriendLogin as Login, Users.Name FROM Friends JOIN Users ON Users.Login = Friends.FriendLogin WHERE Friends.UserLogin = @login";
            var parameters = new Dictionary<string, object>
            {
                { "@login", User.Identity!.Name! }
            };

            return Ok(await _dBSqlExecuter.GetJsonResult(query, parameters));
        }

        [HttpDelete("RemoveFriend")]
        [Authorize]
        public async Task<ActionResult> DeleteFrinedAsync(string friendLogin)
        {
            var query = "DELETE FROM Friends WHERE UserLogin = @login AND FriendLogin = @friend";
            var parameters = new Dictionary<string, object>
            {
                { "@login", User.Identity!.Name! },
                { "@friend", friendLogin }
            };

            if (await _dBSqlExecuter.ExecuteQuery(query, parameters) == 2)
                return Ok();

            return BadRequest();
        }

        [HttpGet("Friends/ByName")]
        [Authorize]
        public async Task<ActionResult> GetFrinedsByNameAsync(string name)
        {
            var query = @$"SELECT Friends.FriendLogin as Login, Users.Name FROM Friends 
                        JOIN Users ON Users.Login = Friends.FriendLogin 
                        WHERE Friends.UserLogin = @login AND Users.Name LIKE '%{name}%' ";
            var parameters = new Dictionary<string, object>
            {
                { "@login", User.Identity!.Name! }
            };

            return Ok(await _dBSqlExecuter.GetJsonResult(query, parameters));
        }

        #endregion

        #endregion

        #region Utility methods
        private async Task<ClaimsIdentity?> GetIdentity(string login, string password)
        {
            var userRole = await GetUserRoleByAuthData(login, password);

            if (userRole == null)
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

        private async Task<string?> GetUserRoleByAuthData(string login, string password)
        {
            var query = "SELECT Role FROM Users WHERE Login = @login AND Password = @password";
            var parameters = new Dictionary<string, object>();
            parameters.Add("@login", login);
            parameters.Add("@password", password);

            var result = await _dBSqlExecuter.GetJsonResult(query, parameters);

            return (result.FirstOrDefault()?["Role"]?.ToString());
        }

        private async Task<bool> CheLoginAvailability(string login)
        {
            var query = "SELECT Login FROM Users WHERE Login = @login";
            var parameters = new Dictionary<string, object>();
            parameters.Add("@login", login);

            return (await _dBSqlExecuter.GetJsonResult(query, parameters)).Count() == 0;
        }
        #endregion
    }
}

