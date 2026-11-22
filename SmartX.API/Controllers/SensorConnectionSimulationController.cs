using Microsoft.AspNetCore.Mvc;
using SmartX.API.Data;
using SmartX.API.Models;

namespace SmartX.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SensorConnectionSimulationController : ControllerBase
    {
        private readonly SmartXDataStore _dataStore;

        public SensorConnectionSimulationController(SmartXDataStore dataStore)
        {
            _dataStore = dataStore;
        }

        [HttpPost("{sensorId}/disconnect")]
        public IActionResult SimulateDisconnect(Guid sensorId)
        {
            var sensor = _dataStore.Sensors.FirstOrDefault(s => s.Id == sensorId);

            if (sensor == null)
            {
                return NotFound(new
                {
                    message = "Sensor not found."
                });
            }

            // Stop future telemetry from this sensor.
            sensor.TelemetryEnabled = false;

            // Move LastSeen beyond the 15-second timeout.
            sensor.LastSeen = DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(20));

            return Ok(new
            {
                message = "Sensor disconnect simulation started. " + "The monitoring service will detect the sensor ",
                sensor.DeviceIdentifier,
                sensor.TelemetryEnabled,
                sensor.LastSeen
            });
        }

        [HttpPost("{sensorId}/reconnect")]
        public IActionResult SimulateReconnect(Guid sensorId)
        {
            var sensor = _dataStore.Sensors
                .FirstOrDefault(s => s.Id == sensorId);

            if (sensor == null)
            {
                return NotFound(new
                {
                    message = "Sensor not found."
                });
            }

            sensor.TelemetryEnabled = true;
            sensor.LastSeen = DateTime.UtcNow;
            sensor.Status = SensorStatus.Online;

            return Ok(new
            {
                message = "Sensor reconnected successfully. " + "Telemetry generation has been restored.",
                sensor.DeviceIdentifier,
                sensor.TelemetryEnabled,
                sensor.Status,
                sensor.LastSeen
            });
        }
    }
}