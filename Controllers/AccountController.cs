using ChessAPI.Infrastructure;
using ChessAPI.Models;
using Microsoft.AspNetCore.Authorization;
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
        public ActionResult Token(string login, string password)
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

            return Ok(new TokenModel(encodedJwt,login));
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

        [HttpGet]
        [Authorize]
        public ActionResult<User> Get()
        {
            var user = User.Identity?.Name;
            var result = _dbContext.Users.FirstOrDefault(u => u.Data!.Login == user);
            return Ok(result);
        }
        
        [HttpPost]
        public ActionResult CreateAccount(RegistrationModel registrationData)
        {
            var loginIsValid = _dbContext.Users.Where(u => u.Data!.Login == registrationData.Login).Count() == 0;
            if (!loginIsValid)
                ModelState.AddModelError("Login", "Login is already taken");
            
            if (ModelState.IsValid)
            {
                var user = new User
                {
                    Name = registrationData.Name,
                    Data = new()
                    {
                        Login = registrationData.Login,
                        Password = registrationData.Password,
                        Role = "User"
                    }
                };
                _dbContext.Users.Add(user);
                _dbContext.SaveChanges();
                return Ok("Account has been create. You can get token.");
            }

            return ValidationProblem();
        }
        
        [HttpPut]
        [Authorize]
        public ActionResult UpdateAccount(User newData)
        {
            var user = _dbContext.Users.FirstOrDefault(u => u.Data!.Login == User.Identity!.Name);
            if (user == null) return BadRequest();
            user.Name = newData.Name;
            user.Data!.Password = newData.Data!.Password;
            _dbContext.SaveChanges();
            return Ok(user);
        }

        [HttpDelete]
        [Authorize]
        public ActionResult DeleteUser()
        {
            var user = _dbContext.Users.FirstOrDefault(u => u.Data!.Login == User.Identity!.Name);
            if (user == null) return BadRequest();

            _dbContext.Users.Remove(user);
            _dbContext.SaveChanges();

            return Ok();
        }

        [HttpDelete]
        [Route("/{id}")]
        [Authorize(Roles = "Admin")]
        public ActionResult DeleteUser(int id)
        {
            var user = _dbContext.Users.FirstOrDefault(u => u.Id == id);
            if (user == null) return NotFound();

            _dbContext.Users.Remove(user);
            _dbContext.SaveChanges();

            return Ok();
        }

        [HttpGet]
        [Route("/UserData/{login}")]
        [Authorize(Roles = "Admin")]
        public ActionResult GetUserData(string login)
        {
            var result = _dbContext.Users.FirstOrDefault(u => u.Data!.Login == login);

            if (result == null) return NotFound("User not found");

            return Ok(result);
        }
        [HttpGet]
        [Route("/UserData")]
        [Authorize(Roles = "Admin")]
        public ActionResult<IEnumerable<User>> GetUsersData()
        {
            var result = _dbContext.Users.Select(u => u).ToList();

            return Ok(result);
        }
    }
}
