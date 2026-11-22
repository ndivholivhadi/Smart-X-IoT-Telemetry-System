using Microsoft.AspNetCore.Mvc;
using SmartX.API.Models;
using SmartX.API.Services;

namespace SmartX.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TelemetryController : ControllerBase
    {
        private readonly TelemetryService _telemetryService;

        public TelemetryController(TelemetryService telemetryService)
        {
            _telemetryService = telemetryService;
        }

        [HttpPost("soil-moisture")]
        public IActionResult AddSoilMoisture(TelemetryPacket<float> packet)
        {
            _telemetryService.AddFloatTelemetry(packet);

            return Ok(packet);
        }

        [HttpPost("power")]
        public IActionResult AddPower(TelemetryPacket<int> packet)
        {
            _telemetryService.AddIntegerTelemetry(packet);

            return Ok(packet);
        }

        [HttpPost("valve")]
        public IActionResult AddValveState(TelemetryPacket<bool> packet)
        {
            _telemetryService.AddBooleanTelemetry(packet);

            return Ok(packet);
        }

        [HttpGet("soil-moisture")]
        public IActionResult GetSoilMoisture()
        {
            return Ok(_telemetryService.GetFloatTelemetry());
        }

        [HttpGet("power")]
        public IActionResult GetPower()
        {
            return Ok(_telemetryService.GetIntegerTelemetry());
        }

        [HttpGet("valve")]
        public IActionResult GetValveState()
        {
            return Ok(_telemetryService.GetBooleanTelemetry());
        }

        [HttpGet("soil-moisture/history")]
        public IActionResult GetSoilMoistureHistory([FromQuery] string? deviceIdentifier = null)
        {
            return Ok(

                _telemetryService.GetFloatTelemetryHistory(deviceIdentifier));
        }

        [HttpGet("power/history")]
        public IActionResult GetPowerConsumptionHistory([FromQuery] string? deviceIdentifier = null)
        {
            return Ok(

                _telemetryService.GetIntegerTelemetryHistory(deviceIdentifier));
        }

        [HttpGet("valve/history")]
        public IActionResult GetValveHistory([FromQuery] string? deviceIdentifier = null)
        {
            return Ok(

                _telemetryService.GetBooleanTelemetryHistory(deviceIdentifier));
        }
    }
}