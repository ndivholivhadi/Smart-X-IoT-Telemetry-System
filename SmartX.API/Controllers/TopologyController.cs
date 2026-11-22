using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SmartX.API.Data;
using SmartX.API.Models;
using SmartX.API.Services;

namespace SmartX.API.Controllers
{
    [ApiController]
    [Route("api/topology")]
    public class TopologyController : ControllerBase
    {
        private readonly MeshTopologyService _topology;
        private readonly SmartXDataStore _dataStore;

        public TopologyController(MeshTopologyService topology, SmartXDataStore dataStore)
        {
            _topology = topology;
            _dataStore = dataStore;
        }

        // GET api/topology : the whole mesh (nodes, links, MST links, down nodes, re-routes)
        [HttpGet]
        public ActionResult<TopologySnapshot> GetTopology() => Ok(_topology.GetSnapshot());

        // GET api/topology/route?identifier=ESP32-001  (or a MAC address)
        [HttpGet("route")]
        public ActionResult<RouteTrace> TraceRoute([FromQuery] string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return BadRequest(new { message = "Enter a device identifier or MAC address." });

            return Ok(_topology.TraceRoute(identifier));
        }

        // GET api/topology/connected?from=GATEWAY-01 : BFS from the gateway, or from any node
        [HttpGet("connected")]
        public ActionResult<ConnectivityReport> GetConnected([FromQuery] string? from = null)
            => Ok(_topology.GetConnectivity(from));

        // GET api/topology/history?identifier=ESP32-001&metric=soil&from=...&to=...
        // metric: soil | power | valve. Uses the device's AVL tree, so only the requested window is visited.
        [HttpGet("history")]
        public ActionResult<HistoryQueryResult> GetHistory(
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

            var fromUtc = (from ?? DateTimeOffset.UtcNow.AddHours(-1)).UtcDateTime;
            var toUtc = (to ?? DateTimeOffset.UtcNow).UtcDateTime;

            if (fromUtc > toUtc)
                return BadRequest(new { message = "The start time must be before the end time." });

            var sensor = _dataStore.FindSensor(identifier);
            var device = sensor?.DeviceIdentifier ?? identifier.Trim();

            var stopwatch = Stopwatch.StartNew();
            List<HistoryReading> readings;

            switch (metric)
            {
                case "soil":
                    readings = _dataStore.GetFloatTelemetryRange(device, fromUtc, toUtc)
                        .Select(p => new HistoryReading { Timestamp = p.Timestamp, Value = p.Value.ToString("F1"), Unit = p.Unit })
                        .ToList();
                    break;
                case "power":
                    readings = _dataStore.GetIntegerTelemetryRange(device, fromUtc, toUtc)
                        .Select(p => new HistoryReading { Timestamp = p.Timestamp, Value = p.Value.ToString(), Unit = p.Unit })
                        .ToList();
                    break;
                default:
                    readings = _dataStore.GetBooleanTelemetryRange(device, fromUtc, toUtc)
                        .Select(p => new HistoryReading { Timestamp = p.Timestamp, Value = p.Value ? "ON" : "OFF", Unit = p.Unit })
                        .ToList();
                    break;
            }

            stopwatch.Stop();
            long micros = stopwatch.ElapsedTicks * 1_000_000 / Stopwatch.Frequency;

            var shape = _dataStore.GetHistoryShape(metric, device);

            // An operator action: feeds the predictive engine.
            _dataStore.RecordTelemetrySearch(new TelemetrySearchRecord
            {
                DeviceIdentifier = device,
                TelemetryCategory = "History",
                SearchedAt = DateTime.UtcNow,
                ResultsFound = readings.Count,
                SearchTimeMicroseconds = micros
            });

            return Ok(new HistoryQueryResult
            {
                DeviceIdentifier = device,
                Mac = sensor?.MacAddress,
                Metric = metric,
                From = fromUtc,
                To = toUtc,
                ResultsFound = readings.Count,
                TotalStored = shape.Count,
                TreeHeight = shape.Height,
                SearchTimeMicroseconds = micros,
                Readings = readings
            });
        }

        // POST api/topology/seed-demo : registers nine demo sensors in three zones (skips ones that exist)
        [HttpPost("seed-demo")]
        public IActionResult SeedDemoNetwork()
        {
            var demo = new (string Id, string Zone, SensorCategory Category)[]
            {
                ("MESH-001", "Greenhouse-A", SensorCategory.Environmental),
                ("MESH-002", "Greenhouse-A", SensorCategory.Environmental),
                ("MESH-003", "Greenhouse-A", SensorCategory.Actuator),
                ("MESH-004", "Power Room",   SensorCategory.PowerConsumption),
                ("MESH-005", "Power Room",   SensorCategory.PowerConsumption),
                ("MESH-006", "Power Room",   SensorCategory.Environmental),
                ("MESH-007", "Irrigation-A", SensorCategory.Actuator),
                ("MESH-008", "Irrigation-A", SensorCategory.Environmental),
                ("MESH-009", "Irrigation-A", SensorCategory.PowerConsumption)
            };

            int added = 0;

            foreach (var (id, zone, category) in demo)
            {
                if (_dataStore.SensorLookup.ContainsKey(id))
                    continue;

                var sensor = new Sensor
                {
                    DeviceIdentifier = id,
                    DeploymentLocation = "Demo Hydroponic Farm",
                    Zone = zone,
                    Category = category
                };

                _dataStore.Sensors.Add(sensor);
                _dataStore.IndexSensor(sensor);
                added++;
            }

            if (added > 0)
                _dataStore.SaveSensors();

            return Ok(new { added, message = added > 0 ? $"Registered {added} demo sensors." : "Demo sensors already exist." });
        }
    }
}
