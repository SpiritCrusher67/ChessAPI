using ChessAPI.Infrastructure;
using ChessAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace ChessAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PostController : ControllerBase
    {
        IDBSqlExecuter _dBSqlExecuter;
        public PostController(IDBSqlExecuter dBSqlExecuter)
        {
            _dBSqlExecuter = dBSqlExecuter;
        }

        [HttpGet("/News")]
        public async Task<ActionResult> GetNewsAsync(int page, int limit) => await GetAsync("admin", page, limit);

        #region Likes acions
        [Authorize]
        [HttpGet("/SetLike/{id:int}")]
        public async Task<ActionResult> SetLikeAsync(int id)
        {
            var query = "INSERT INTO PostLikes VALUES (@id, @login, DEFAULT)";
            var parameters = new Dictionary<string, object>
            {
                { "@id", id },
                { "@login", User.Identity!.Name! }
            };

            if (await _dBSqlExecuter.ExecuteQuery(query, parameters) != 1)
                return BadRequest(new { errorText = "You already liked this post." });

            return Ok();
        }

        [Authorize]
        [HttpDelete("/RemoveLike/{id:int}")]
        public async Task<ActionResult> DeleteLikeAsync(int id)
        {
            var query = "DELETE FROM PostLikes WHERE UserLogin = @login AND PostId = @id";
            var parameters = new Dictionary<string, object>
            {
                { "@id", id },
                { "@login", User.Identity!.Name! }
            };

            if (await _dBSqlExecuter.ExecuteQuery(query, parameters) == 1)
                return Ok();

            return BadRequest();
        }
        #endregion

        #region Comments acions
        [Authorize]
        [HttpPost("/CreateComment")]
        public async Task<ActionResult> CreateCommentAsync(int id, [StringLength(70, MinimumLength = 5)] string comment)
        {
            if (!ModelState.IsValid)
                return ValidationProblem();

            var query = "INSERT INTO PostComments VALUES (@id, @login, @text, DEFAULT)";
            var parameters = new Dictionary<string, object>
            {
                { "@id", id },
                { "@login", User.Identity!.Name! },
                { "@text", comment }
            };

            if (await _dBSqlExecuter.ExecuteQuery(query, parameters) == 1)
                return Ok();

            return BadRequest();
        }

        [Authorize]
        [HttpDelete("/RemoveComment/{id:int}")]
        public async Task<ActionResult> DeleteCommentAsync(int id)
        {
            var query = "DELETE FROM PostComments WHERE UserLogin = @login AND Id = @id";
            var parameters = new Dictionary<string, object>
            {
                { "@id", id },
                { "@login", User.Identity!.Name! }
            };

            if (await _dBSqlExecuter.ExecuteQuery(query, parameters) == 1)
                return Ok();

            return BadRequest();
        }
        #endregion

        #region Post actions
        [HttpGet("{id:int}")]
        [Authorize]
        public async Task<ActionResult> GetAsync(int id)
        {
            var query = "SELECT * FROM Posts WHERE Id = @id";
            var parameters = new Dictionary<string, object>();
            parameters.Add("@id", id);

            var result = await _dBSqlExecuter.GetJsonResult(query, parameters);
            return Ok(result);
        }

        [HttpGet("{login}")]
        //[Authorize]
        public async Task<ActionResult> GetAsync(string login, int page, int limit)
        {
            if (page <= 0 || limit <= 0)
                return BadRequest(new { errorText = "page and limit must be > 0" });
            var userLogin = User.Identity?.Name ?? string.Empty;

            var query = @$"SELECT *, (SELECT COUNT(Id) FROM PostComments WHERE PostId = Posts.Id) AS Comments,
                           (SELECT COUNT(PostLikes.PostId) FROM PostLikes WHERE PostId = Posts.Id) as Likes, 
                           (select IIF( (SELECT COUNT(PostLikes.PostId) FROM PostLikes WHERE UserLogin = @user) > 0,'true','false')) as Liked 
                           FROM Posts ORDER BY Date DESC OFFSET {page * limit - limit} ROWS FETCH NEXT {limit} ROWS ONLY;
                           SET @count = (SELECT Count(Id) FROM Posts WHERE UserLogin = @login)";
            var parameters = new Dictionary<string, object>
            {
                { "@login", login },
                { "@user", userLogin },
            };

            var result = await _dBSqlExecuter.ExecuteQueryOutIntParameter(query, "@count", parameters);

            var totalPages = Math.Ceiling((decimal)result.Item2 / limit);

            Response.Headers.Append("totalPages", totalPages.ToString());
            Response.Headers.Append("page", page.ToString());
            Response.Headers.Append("limit", limit.ToString());

            return Ok(result.Item1);
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult> Post(PostModel model)
        {
            if (!ModelState.IsValid)
                return ValidationProblem();

            var query = "INSERT INTO Posts VALUES (@login, @title, @text, @tags, DEFAULT)";
            var parameters = new Dictionary<string, object> 
            {
                { "@login", User.Identity!.Name! },
                { "@title", model.Title },
                { "@text", model.Text },
                { "@tags", model.Tags }
            };

            if (await _dBSqlExecuter.ExecuteQuery(query, parameters) == 1)
                return Ok();

            return BadRequest();
        }

        [Authorize]
        [HttpDelete("{id:int}")]
        public async Task<ActionResult> DeleteAsync(int id)
        {
            var login = User.Identity!.Name;

            var query = "DELETE FROM Posts WHERE Id = @id AND UserLogin = @login";
            var parameters = new Dictionary<string, object>
            {
                { "@login", login! },
                { "@id", id }
            };

            if (await _dBSqlExecuter.ExecuteQuery(query, parameters) == 1)
                return Ok();

            return NotFound();
        }
        #endregion
    }
}
