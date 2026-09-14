using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Widgets.AgentSearch.Models;
using Nop.Plugin.Widgets.AgentSearch.Services;
using Nop.Services.Configuration;

namespace Nop.Plugin.Widgets.AgentSearch.Controllers
{
    [ApiController]
    [Route("api/agent/search")]
    public class AgentSearchApiController : ControllerBase
    {
        private readonly IAgentSearchService _agentSearchService;
        private readonly ISettingService _settingService;

        public AgentSearchApiController(
            IAgentSearchService agentSearchService,
            ISettingService settingService)
        {
            _agentSearchService = agentSearchService;
            _settingService = settingService;
        }

        [HttpPost]
        public async Task<IActionResult> Search([FromBody] AgentSearchRequest request)
        {
            var settings = await _settingService.LoadSettingAsync<AgentSearchSettings>();

            if (!settings.Enabled)
                return NotFound();

            if (settings.RequireApiKey)
            {
                var providedKey = Request.Headers["X-Api-Key"].ToString();
                if (string.IsNullOrEmpty(providedKey) || providedKey != settings.ApiKey)
                {
                    return Unauthorized(new AgentSearchErrorResponse
                    {
                        Error = "unauthorized",
                        Detail = "Missing or invalid X-Api-Key header."
                    });
                }
            }

            if (request == null || string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest(new AgentSearchErrorResponse
                {
                    Error = "invalid_request",
                    Detail = "The 'query' field is required."
                });
            }

            var response = await _agentSearchService.SearchAsync(request, settings);
            return Ok(response);
        }
    }
}
