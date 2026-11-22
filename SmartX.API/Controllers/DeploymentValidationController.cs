using Microsoft.AspNetCore.Mvc;
using SmartX.API.Models;
using SmartX.API.Services;

namespace SmartX.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DeploymentValidationController : ControllerBase
    {
        private readonly DeploymentValidationService _validationService;

        public DeploymentValidationController(DeploymentValidationService validationService)
        {
            _validationService = validationService;
        }

        [HttpPost]
        public IActionResult Validate([FromBody] DeploymentNode deployment)
        {
            var result = _validationService.ValidateDeploymentTree(deployment);

            return Ok(new
            {
                result.Valid,
                result.Message,
                result.NodesVisited,
                result.MaximumDepth,
                algorithm = "Recursive tree traversal"
            });
        }
    }
}