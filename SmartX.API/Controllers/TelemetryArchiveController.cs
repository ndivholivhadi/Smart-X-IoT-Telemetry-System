using Microsoft.AspNetCore.Mvc;
using SmartX.API.Data;
using SmartX.API.Structures;

namespace SmartX.API.Controllers
{
    [ApiController]
    [Route("api/telemetry-archive")]
    public class TelemetryArchiveController : ControllerBase
    {
        private readonly SmartXDataStore _dataStore;

        public TelemetryArchiveController(SmartXDataStore dataStore)
        {
            _dataStore = dataStore;
        }

        // GET api/telemetry-archive?identifier=ESP32-001&metric=soil&from=...&to=...
        [HttpGet]
        public ActionResult<IEnumerable<HourlySummary>> GetArchive(
            [FromQuery] string identifier,
            [FromQuery] string metric = "soil",
            [FromQuery] DateTimeOffset? from = null,
            [FromQuery] DateTimeOffset? to = null)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return BadRequest(new { message = "Enter a device identifier or MAC address." });

            metric = metric.Trim().ToLowerInvariant();

            if (metric is not ("soil" or "power" or "valve"))
                return BadRequest(new { message = "Metric must be soil, power or valve." });

            var fromUtc = (from ?? DateTimeOffset.UtcNow.AddDays(-7)).UtcDateTime;
            var toUtc = (to ?? DateTimeOffset.UtcNow).UtcDateTime;

            if (fromUtc > toUtc)
                return BadRequest(new { message = "The start time must be before the end time." });

            var device = _dataStore.FindSensor(identifier)?.DeviceIdentifier ?? identifier.Trim();

            return Ok(_dataStore.GetArchive(metric, device, fromUtc, toUtc));
        }
    }
}
