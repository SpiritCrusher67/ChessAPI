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
        OnlineUsersService _onlineUsersService;
        public AccountController(IDBSqlExecuter dBSqlExecuter, OnlineUsersService onlineUsersService, IWebHostEnvironment appEnvironment)
        {
            _dBSqlExecuter = dBSqlExecuter;
            _appEnvironment = appEnvironment;
            _onlineUsersService = onlineUsersService;
        }

        #region Public actions

        [HttpPost]
        public async Task<ActionResult> CreateAccount([FromForm]RegistrationModel registrationData)
        {
            if (!await CheLoginAvailability(registrationData.Login))
                ModelState.AddModelError("Login", "Login is already taken");

            var t = registrationData.ProfileImage?.GetType();

            if (ModelState.IsValid)
            {
                var query = "EXECUTE @res = CreateUser @login, @password, @name, @role";
                var parameters = new Dictionary<string, object>
                {
                    { "@login", registrationData.Login },
                    { "@password", registrationData.Password },
                    { "@name", registrationData.Name },
                    { "@role", "User" }
                };

                if ((await _dBSqlExecuter.ExecuteQueryOutIntParameter(query,"@res", parameters)).Item2 == 1)
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
            var userLogin = User.Identity?.Name;
            var query = @"SELECT *,
                        (SELECT COUNT(*) FROM GamesResults WHERE WinUserLogin = @login OR LoseUserLogin = @login) AS GamesCount,
                        (SELECT COUNT(*) FROM GamesResults WHERE WinUserLogin = @login) AS WinsCount
                        FROM Users
                        WHERE Login = @login";
            var parameters = new Dictionary<string, object>();
            parameters.Add("@login", userLogin!);

            var result = await _dBSqlExecuter.GetJsonResult(query, parameters);

            foreach (var user in result)
                user.Add("IsUserOnline", _onlineUsersService.IsUserOnline(userLogin));

            return Ok(result);
        }

        [HttpDelete]
        [Authorize]
        public async Task<ActionResult> DeleteUserAsync([FromBody]string password)
        {
            var login = User.Identity!.Name!;

            var query = "EXEC @result = TryAuthorize @login, @password";
            var parameters = new Dictionary<string, object>
            {
                { "@login", login },
                { "@password", password}
            };

            if ((await _dBSqlExecuter.ExecuteQueryOutIntParameter(query, "@result", parameters)).Item2 != 1)
                return Unauthorized();

            query = "DELETE FROM UsersAuthorizationData WHERE Login = @login";
            parameters = new Dictionary<string, object>
            {
                { "@login", login! }
            };

            if (await _dBSqlExecuter.ExecuteQuery(query, parameters) > 0)
                return Ok();

            return BadRequest();
        }

        [HttpGet("Users/ByName")]
        [Authorize]
        public async Task<ActionResult> GetUsersByNameAsync(string name)
        {
            var query = $@"SELECT *,
                        (SELECT COUNT(*) FROM GamesResults WHERE WinUserLogin = Login OR LoseUserLogin = Login) AS GamesCount,
                        (SELECT COUNT(*) FROM GamesResults WHERE WinUserLogin = Login) AS WinsCount
                        FROM Users
                        WHERE Name LIKE '%{name}%'";

            var result = await _dBSqlExecuter.GetJsonResult(query);

            foreach (var user in result)
            {
                var userLogin = user["Login"].ToString().Trim();
                user.Add("IsUserOnline", _onlineUsersService.IsUserOnline(userLogin));
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("UserData/{login}")]
        [Authorize]
        public async Task<ActionResult> GetUserDataAsync(string login)
        {
            var userLogin = User.Identity?.Name;
            var query = @"SELECT *,
                        (SELECT COUNT(*) FROM GamesResults WHERE WinUserLogin = @login OR LoseUserLogin = @login) AS GamesCount,
                        (SELECT COUNT(*) FROM GamesResults WHERE WinUserLogin = @login) AS WinsCount,
                        (SELECT COUNT(*) FROM Friends WHERE UserLogin = @login AND FriendLogin = @userLogin) AS IsFriend
                        FROM Users
                        WHERE Login = @login";
            var parameters = new Dictionary<string, object>
            {
                { "@login", login! },
                { "@userLogin", userLogin! }
            };

            var result = await _dBSqlExecuter.GetJsonResult(query, parameters);

            if (result.Count() == 0)
                return BadRequest(new { errorText = "User not found." });

            foreach (var user in result)
                user.Add("IsUserOnline", _onlineUsersService.IsUserOnline(login));

            return Ok(result);
        }

        #region Friend actions 

        [HttpGet("GetFriends")]
        [Authorize]
        public async Task<ActionResult> GetFriendsListAsync()
        {
            var query = @"SELECT Friends.FriendLogin as Login, Users.Name,
                        (SELECT COUNT(*) FROM GamesResults WHERE WinUserLogin = Friends.FriendLogin OR LoseUserLogin = Friends.FriendLogin) AS GamesCount,
                        (SELECT COUNT(*) FROM GamesResults WHERE WinUserLogin = Friends.FriendLogin) AS WinsCount
                        FROM Friends 
                        JOIN Users ON Users.Login = Friends.FriendLogin 
                        WHERE Friends.UserLogin = @login";
            var parameters = new Dictionary<string, object>
            {
                { "@login", User.Identity!.Name! }
            };

            var result = await _dBSqlExecuter.GetJsonResult(query,parameters);

            foreach (var user in result)
            {
                var userLogin = user["Login"].ToString().Trim();
                user.Add("IsUserOnline", _onlineUsersService.IsUserOnline(userLogin));
            }

            return Ok(result);
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
            var query = $@"SELECT Friends.FriendLogin as Login, Users.Name,
                        (SELECT COUNT(*) FROM GamesResults WHERE WinUserLogin = Friends.FriendLogin OR LoseUserLogin = Friends.FriendLogin) AS GamesCount,
                        (SELECT COUNT(*) FROM GamesResults WHERE WinUserLogin = Friends.FriendLogin) AS WinsCount
                        FROM Friends 
                        JOIN Users ON Users.Login = Friends.FriendLogin 
                        WHERE Friends.UserLogin = @login AND Users.Name LIKE '%{name}%'";
            var parameters = new Dictionary<string, object>
            {
                { "@login", User.Identity!.Name! }
            };

            var result = await _dBSqlExecuter.GetJsonResult(query, parameters);

            foreach (var user in result)
            {
                var userLogin = user["Login"].ToString().Trim();
                user.Add("IsUserOnline", _onlineUsersService.IsUserOnline(userLogin));
            }

            return Ok(result);
        }

        #endregion

        #endregion

        #region Utility methods

        private async Task<string?> GetUserRoleByAuthData(string login, string password)
        {
            var query = "EXEC @result = TryAuthorize @login, @password";
            var parameters = new Dictionary<string, object>
            {
                { "@login", login },
                { "@password", password }
            };

            if ((await _dBSqlExecuter.ExecuteQueryOutIntParameter(query,"@result",parameters)).Item2 == 1)
            {
                parameters.Remove("@password");
                query = "SELECT Role FROM Users WHERE Login = @login";
                var result = await _dBSqlExecuter.GetJsonResult(query, parameters);
                return (result.FirstOrDefault()?["Role"]?.ToString());
            }
            return string.Empty;
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

