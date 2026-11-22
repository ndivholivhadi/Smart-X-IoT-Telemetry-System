using Microsoft.AspNetCore.Mvc;
using SmartX.API.Services;

namespace SmartX.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TelemetrySearchController : ControllerBase
    {
        private readonly TelemetrySearchService _searchService;

        public TelemetrySearchController(TelemetrySearchService searchService)
        {
            _searchService = searchService;
        }

        [HttpGet("soil-moisture")]
        public IActionResult SearchSoilMoisture([FromQuery] string deviceIdentifier)
        {
            if (string.IsNullOrWhiteSpace(deviceIdentifier))
            {
                return BadRequest(new
                {
                    message = "Device identifier is required."
                });
            }

            return Ok(_searchService.SearchSoilMoisture(deviceIdentifier));
        }

        [HttpGet("power")]
        public IActionResult SearchPower([FromQuery] string deviceIdentifier)
        {
            if (string.IsNullOrWhiteSpace(deviceIdentifier))
            {
                return BadRequest(new
                {
                    message = "Device identifier is required."
                });
            }

            return Ok(
                _searchService.SearchPower(
                    deviceIdentifier));
        }

        [HttpGet("valve")]
        public IActionResult SearchValve([FromQuery] string deviceIdentifier)
        {
            if (string.IsNullOrWhiteSpace(deviceIdentifier))
            {
                return BadRequest(new
                {
                    message = "Device identifier is required."
                });
            }

            return Ok(
                _searchService.SearchValve(
                    deviceIdentifier));
        }
    }
}