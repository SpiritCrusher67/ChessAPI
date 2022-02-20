using ChessAPI.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace ChessAPI.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        IDBSqlExecuter _dBSqlExecuter;
        public ChatController(IDBSqlExecuter dBSqlExecuter)
        {
            _dBSqlExecuter = dBSqlExecuter;
        }
        #region Chat actions
        [HttpGet]
        public async Task<ActionResult> GetChats()
        {
            var query = @"SELECT Id, Users.Name, Users.Login FROM Chats 
                        JOIN ChatUsers ON ChatUsers.ChatId = Chats.Id
                        JOIN Users ON Users.Login = (SELECT UserLogin FROM ChatUsers WHERE ChatUsers.ChatId = Chats.Id AND ChatUsers.UserLogin <> @login)
                        WHERE ChatUsers.UserLogin = @login";
            var parameters = new Dictionary<string, object>
            {
                { "@login", User.Identity!.Name! }
            };

            return Ok(await _dBSqlExecuter.GetJsonResult(query, parameters));
        }

        [HttpPost]
        public async Task<ActionResult<int>> GetOrCreateChatWithAsync(string userLogin)
        {
            var login = User.Identity!.Name!;
            if (userLogin == login)
                return BadRequest(new { errorText = "You can't create chat with yourself" });

            return Ok(await GetOrCreateChatAsync(login, userLogin));
        }
        #endregion

        #region Message actions
        [HttpGet("{id}")]
        public async Task<ActionResult> GetAllMessages(int id)
        {
            var query = @"SELECT Messages.Id, Messages.Text, Messages.UserLogin, Messages.Type FROM Messages 
                        JOIN ChatUsers ON ChatUsers.ChatId = Messages.ChatId
                        Where Messages.ChatId = @id AND ChatUsers.UserLogin = @login ";
            var parameters = new Dictionary<string, object>
            {
                { "@login", User.Identity!.Name! },
                { "@id", id }
            };

            var result = await _dBSqlExecuter.GetJsonResult(query, parameters);

            return Ok(result);
        }

        [HttpPost("/Message")]
        public async Task<ActionResult> SendMessageAsync(int chatId, [StringLength(100, MinimumLength = 1)] string message)
        {
            if (!ModelState.IsValid)
                return ValidationProblem();

            var query = "INSERT INTO Messages VALUES (@text, @chatId, @login, DEFAULT)";
            var parameters = new Dictionary<string, object>
            {
                { "@text", message },
                { "@chatId", chatId },
                { "@login", User.Identity!.Name!}
            };

            if (await _dBSqlExecuter.ExecuteQuery(query, parameters) == 2)
                return Ok();

            return BadRequest();
        }
        #endregion

        #region Invite actions
        [HttpPost("/Invite")]
        public async Task<ActionResult> SendInviteToFriendsAsync(string userLogin)
        {
            var login = User.Identity!.Name!;

            if (userLogin == login)
                return BadRequest(new { errorText = "You can't send invvite to yourself" });

            var chatId = await GetOrCreateChatAsync(login, userLogin);

            var query = "INSERT INTO Messages VALUES ('',@chatId, @login, '1')";
            var parameters = new Dictionary<string, object>
            {
                { "@chatId", chatId },
                { "@login", User.Identity!.Name!}
            };

            if (await _dBSqlExecuter.ExecuteQuery(query, parameters) == 1)
                return Ok();

            return BadRequest();
        }

        [HttpPost("/Invite/Accept")]
        public async Task<ActionResult> AcceptInviteAsync(int messageId)
        {
            var query = "EXECUTE AcceptInviteToFriends @id, @login";
            var parameters = new Dictionary<string, object>
            {
                { "@id", messageId },
                { "@login", User.Identity!.Name!}
            };

            await _dBSqlExecuter.ExecuteQuery(query, parameters);
            return Ok();
        }
        [HttpPost("/Invite/Decline")]
        public async Task<ActionResult> DeclineInviteAsync(int messageId)
        {
            var query = "EXECUTE DeclineInviteToFriends @id, @login";
            var parameters = new Dictionary<string, object>
            {
                { "@id", messageId },
                { "@login", User.Identity!.Name!}
            };

            await _dBSqlExecuter.ExecuteQuery(query, parameters);
            return Ok();
        }
        #endregion

        #region Utility methods

        private async Task<int> GetOrCreateChatAsync(string creatorlogin, string userLogin)
        {
            var query = "EXEC @id = CreateOrFindChat @login, @userLogin";
            var parameters = new Dictionary<string, object>
            {
                { "@login", creatorlogin},
                { "@userLogin", userLogin}
            };

            var result = await _dBSqlExecuter.ExecuteQueryOutIntParameter(query, "@id", parameters);

            return result.Item2;
        }

        #endregion
    }
}
