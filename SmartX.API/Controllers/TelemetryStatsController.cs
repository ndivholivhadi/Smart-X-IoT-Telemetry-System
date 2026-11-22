using Microsoft.AspNetCore.Mvc;
using SmartX.API.Services;

namespace SmartX.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TelemetryStatsController : ControllerBase
    {
        private readonly TelemetryService _telemetryService;

        public TelemetryStatsController(TelemetryService telemetryService)
        {
            _telemetryService = telemetryService;
        }

        [HttpGet]
        public IActionResult GetStatistics()
        {
            return Ok(_telemetryService.GetStatistics());
        }

        [HttpGet("device-counts")]
        public IActionResult GetDeviceTelemetryCounts()
        {
            var counts = _telemetryService.GetDeviceTelemetryCounts();

            return Ok(counts);
        }
    }

}