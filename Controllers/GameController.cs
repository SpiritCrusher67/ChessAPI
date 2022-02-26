using ChessAPI.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace ChessAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GameController : ControllerBase 
    {
        IDBSqlExecuter _dBSqlExecuter;
        IWebHostEnvironment _appEnvironment;
        public GameController(IDBSqlExecuter dBSqlExecuter, IWebHostEnvironment appEnvironment)
        {
            _dBSqlExecuter = dBSqlExecuter;
            _appEnvironment = appEnvironment;
        }

        [HttpGet("GetFigureImage")]
        public async Task<ActionResult> GetFigureImage(string side, string type)
        {
            var path = $"{_appEnvironment.WebRootPath}/img/{side}/{type}.png";

            if (System.IO.File.Exists(path))
                return PhysicalFile(path, "image/png");

            return BadRequest();
        }
    }
}
