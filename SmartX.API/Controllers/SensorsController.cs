
using Microsoft.AspNetCore.Mvc;
using SmartX.API.Data;
using SmartX.API.Models;
using SmartX.API.Services;

namespace SmartX.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SensorsController : ControllerBase
    {
        private readonly SmartXDataStore _dataStore;

        public SensorsController(SmartXDataStore dataStore)
        {
            _dataStore = dataStore;
        }

        // GET: api/sensors
        [HttpGet]
        public ActionResult<IEnumerable<Sensor>> GetSensors()
        {
            return Ok(_dataStore.Sensors);
        }

        // GET: api/sensors/{id}
        [HttpGet("{id:guid}")]
        public ActionResult<Sensor> GetSensor(Guid id)
        {
            var sensor = _dataStore.Sensors
                .FirstOrDefault(s => s.Id == id);

            if (sensor == null)
            {
                return NotFound(new
                {
                    message = "Sensor not found."
                });
            }

            return Ok(sensor);
        }

        // POST: api/sensors
        [HttpPost]
        public ActionResult<Sensor> RegisterSensor(
            [FromBody] Sensor sensor)
        {
            // Validate the request. Every problem is reported, not just the first.
            var errors = SensorValidator.Validate(sensor);

            if (errors.Count > 0)
            {
                return BadRequest(new
                {
                    message = string.Join(" ", errors),
                    errors
                });
            }

            sensor.DeviceIdentifier = sensor.DeviceIdentifier.Trim();
            sensor.DeploymentLocation = sensor.DeploymentLocation.Trim();
            sensor.Zone = sensor.Zone.Trim();

            // Check for duplicates using the dictionary.
            if (_dataStore.SensorLookup.ContainsKey(
                sensor.DeviceIdentifier))
            {
                return Conflict(new
                {
                    message = "A sensor with this device identifier already exists."
                });
            }

            if (!string.IsNullOrWhiteSpace(sensor.MacAddress) &&
                _dataStore.MacLookup.ContainsKey(SmartXDataStore.NormalizeMac(sensor.MacAddress)))
            {
                return Conflict(new
                {
                    message = "A sensor with this MAC address already exists."
                });
            }


            sensor.Id = Guid.NewGuid();
            sensor.LastSeen = DateTime.UtcNow;
            sensor.Status = SensorStatus.Online;
            sensor.TelemetryEnabled = true;

            // Add the sensor to the list and dictionary.
            _dataStore.Sensors.Add(sensor);

            _dataStore.IndexSensor(sensor);   // device identifier + MAC lookups

            _dataStore.SaveSensors();

            // Return HTTP 201 Created.
            return CreatedAtAction(
                nameof(GetSensor),
                new { id = sensor.Id },
                sensor);
        }
    }
}
