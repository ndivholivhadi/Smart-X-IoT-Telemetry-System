using Microsoft.AspNetCore.Mvc;
using SmartX.API.Data;
using SmartX.API.Models;
using SmartX.API.Services;

namespace SmartX.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AnomalySimulationController : ControllerBase
    {
        private readonly TelemetryService _telemetryService;
        private readonly AnomalyDetectionService _anomalyDetectionService;
        private readonly SmartXDataStore _dataStore;

        public AnomalySimulationController(
            TelemetryService telemetryService,
            AnomalyDetectionService anomalyDetectionService,
            SmartXDataStore dataStore)
        {
            _telemetryService = telemetryService;
            _anomalyDetectionService = anomalyDetectionService;
            _dataStore = dataStore;
        }

        [HttpPost("soil-moisture")]
        public IActionResult SimulateSoilMoistureAnomaly([FromQuery] string deviceIdentifier, [FromQuery] float value = 10)
        {
            var sensor = _dataStore.Sensors.FirstOrDefault(
                s => s.DeviceIdentifier.Equals(
                    deviceIdentifier,
                    StringComparison.OrdinalIgnoreCase));

            if (sensor == null)
            {
                return NotFound(new
                {
                    message = "Sensor not found."
                });
            }

            var packet = new TelemetryPacket<float>
            {
                DeviceIdentifier = sensor.DeviceIdentifier,
                SensorName = "Soil Moisture Sensor",
                Unit = "%",
                Timestamp = DateTime.UtcNow,
                Value = value
            };

            _telemetryService.AddFloatTelemetry(packet);
            _anomalyDetectionService.CheckSoilMoisture(packet);

            return Ok(new
            {
                message = "Soil moisture anomaly simulation completed.",
                deviceIdentifier = sensor.DeviceIdentifier,
                value = packet.Value,
                threshold = 20.0f,
                anomaly = packet.Value < 20.0f
            });
        }

        [HttpPost("power")]
        public IActionResult SimulatePowerAnomaly([FromQuery] string deviceIdentifier, [FromQuery] int value = 6000)
        {
            var sensor = _dataStore.Sensors.FirstOrDefault(
                s => s.DeviceIdentifier.Equals(
                    deviceIdentifier,
                    StringComparison.OrdinalIgnoreCase));

            if (sensor == null)
            {
                return NotFound(new
                {
                    message = "Sensor not found."
                });
            }

            var packet = new TelemetryPacket<int>
            {
                DeviceIdentifier = sensor.DeviceIdentifier,
                SensorName = "Power Consumption Sensor",
                Unit = "W",
                Timestamp = DateTime.UtcNow,
                Value = value
            };

            _telemetryService.AddIntegerTelemetry(packet);
            _anomalyDetectionService.CheckPowerConsumption(packet);

            return Ok(new
            {
                message = "Power consumption anomaly simulation completed.",
                deviceIdentifier = sensor.DeviceIdentifier,
                value = packet.Value,
                threshold = 5000,
                anomaly = packet.Value > 5000
            });
        }
    }
}