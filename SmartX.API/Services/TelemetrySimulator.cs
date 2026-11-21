using SmartX.API.Data;
using SmartX.API.Models;

namespace SmartX.API.Services
{
    public class TelemetrySimulator
    {
        private readonly TelemetryService _telemetryService;
        private readonly AnomalyDetectionService _anomalyDetectionService;
        private readonly SmartXDataStore _dataStore;
        private readonly Random _random = new();

        private int _generationCount = 0;

        public TelemetrySimulator(
            TelemetryService telemetryService,
            AnomalyDetectionService anomalyDetectionService,
            SmartXDataStore dataStore)
        {
            _telemetryService = telemetryService;
            _anomalyDetectionService = anomalyDetectionService;
            _dataStore = dataStore;
        }

        public void GenerateTelemetry()
        {
            _generationCount++;

            GenerateSoilMoisture();
            GeneratePowerConsumption();
            GenerateValveState();
        }

        private void GenerateSoilMoisture()
        {
            var sensors = _dataStore.Sensors.Where(s => s.Category == SensorCategory.Environmental && s.TelemetryEnabled).ToList();

            foreach (var sensor in sensors)
            {
                float value;

                if (_generationCount % 12 == 0)
                {
                    // Deliberate anomaly for demonstration/testing
                    value = (float)(_random.NextDouble() * 5 + 5);
                }
                else
                {
                    // Normal reading: approximately 30% - 70%
                    value = (float)(_random.NextDouble() * 40 + 30);
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

                sensor.LastSeen = DateTime.UtcNow;
                sensor.Status = SensorStatus.Online;
            }
        }

        private void GeneratePowerConsumption()
        {

            var sensors = _dataStore.Sensors
                .Where(s =>
                    s.Category == SensorCategory.PowerConsumption &&
                    s.TelemetryEnabled)
                .ToList();

            foreach (var sensor in sensors)
            {
                int value;

                if (_generationCount % 10 == 0)
                {
                    // Deliberate abnormal power spike
                    value = _random.Next(7000, 9001);
                }
                else
                {
                    // Normal power consumption
                    value = _random.Next(1500, 2201);
                }

                var packet = new TelemetryPacket<int>
                {
                    DeviceIdentifier = sensor.DeviceIdentifier,
                    SensorName = "Power Meter",
                    Unit = "W",
                    Timestamp = DateTime.UtcNow,
                    Value = value
                };

                _telemetryService.AddIntegerTelemetry(packet);
                _anomalyDetectionService.CheckPowerConsumption(packet);

                sensor.LastSeen = DateTime.UtcNow;
                sensor.Status = SensorStatus.Online;
            }
        }

        private void GenerateValveState()
        {
            var sensors = _dataStore.Sensors
             .Where(s =>
                 s.Category == SensorCategory.Actuator &&
                 s.TelemetryEnabled)
             .ToList();

            foreach (var sensor in sensors)
            {
                bool value = _random.Next(0, 2) == 1;

                var packet = new TelemetryPacket<bool>
                {
                    DeviceIdentifier = sensor.DeviceIdentifier,
                    SensorName = "Irrigation Valve",
                    Unit = "state",
                    Timestamp = DateTime.UtcNow,
                    Value = value
                };

                _telemetryService.AddBooleanTelemetry(packet);

                sensor.LastSeen = DateTime.UtcNow;
                sensor.Status = SensorStatus.Online;
            }
        }
    }
}