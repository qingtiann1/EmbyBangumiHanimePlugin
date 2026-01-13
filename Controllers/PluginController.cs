using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Mvc;

namespace EmbyBangumiHanimePlugin.Controllers
{
    [ApiController]
    [Route("Plugins/EmbyBangumiHanimePlugin")]
    public class PluginController : ControllerBase
    {
        private readonly Plugin _plugin;
        
        public PluginController()
        {
            _plugin = Plugin.Instance;
        }
        
        [HttpPost("AuthenticateBangumi")]
        public async Task<IActionResult> AuthenticateBangumi([FromQuery] string code, CancellationToken cancellationToken)
        {
            var result = await _plugin.AuthenticateBangumiAsync(code, cancellationToken);
            return result ? Ok() : BadRequest();
        }
        
        [HttpPost("AuthenticateHanime")]
        public async Task<IActionResult> AuthenticateHanime([FromBody] LoginRequest request, CancellationToken cancellationToken)
        {
            var result = await _plugin.AuthenticateHanimeAsync(request.Username, request.Password, cancellationToken);
            return result ? Ok() : BadRequest();
        }
        
        [HttpPost("AuthenticateHanimeWithCookie")]
        public async Task<IActionResult> AuthenticateHanimeWithCookie([FromBody] CookieRequest request, CancellationToken cancellationToken)
        {
            var result = await _plugin.AuthenticateHanimeWithCookieAsync(request.Cookie, cancellationToken);
            return result ? Ok() : BadRequest();
        }
        
        [HttpPost("Configuration")]
        public IActionResult UpdateConfiguration([FromBody] PluginConfiguration config)
        {
            // 更新配置
            return Ok();
        }
        
        [HttpGet("Configuration")]
        public IActionResult GetConfiguration()
        {
            return Ok(_plugin.Configuration);
        }
        
        [HttpPost("Scrape")]
        public async Task<IActionResult> ScrapeMetadata([FromBody] ScrapeRequest request, CancellationToken cancellationToken)
        {
            var result = await _plugin.ScrapeMetadataAsync(request.Title, request.UseBangumi, request.UseHanime, cancellationToken);
            return Ok(result);
        }
    }
    
    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }
    
    public class CookieRequest
    {
        public string Cookie { get; set; }
    }
    
    public class ScrapeRequest
    {
        public string Title { get; set; }
        public bool UseBangumi { get; set; } = true;
        public bool UseHanime { get; set; } = true;
    }
}
