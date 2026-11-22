
using Microsoft.AspNetCore.Mvc;
using SmartX.API.Services;

namespace SmartX.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RecommendationsController : ControllerBase
    {
        private readonly RecommendationService _service;

        public RecommendationsController(RecommendationService service)
        {
            _service = service;
        }

        [HttpGet]
        public IActionResult GetRecommendations()
        {
            return Ok(_service.GetRecommendations());
        }
    }
}
