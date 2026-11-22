using Microsoft.AspNetCore.Mvc;
using SmartX.API.Services;

namespace SmartX.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TelemetrySimulationController : ControllerBase
    {
        private readonly TelemetrySimulationService _simulationService;

        public TelemetrySimulationController(TelemetrySimulationService simulationService)
        {
            _simulationService = simulationService;
        }

        [HttpPost("thousands")]
        public IActionResult SimulateThousands([FromQuery] int deviceCount = 1000)
        {
            try
            {
                if (deviceCount < 1000)
                {
                    return BadRequest(new
                    {
                        message = "This endpoint is intended for large-scale simulation. " + "Device count must be at least 1000."
                    });
                }

                var result = _simulationService.SimulateTelemetry(deviceCount);

                return Ok(new
                {
                    message = "Large-scale telemetry simulation completed successfully.",
                    result.DevicesSimulated,
                    result.TelemetryRecordsGenerated,
                    result.DurationMilliseconds,
                    result.AverageRecordsPerDevice
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
        }
    }
}