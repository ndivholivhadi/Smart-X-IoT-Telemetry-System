using SmartX.API.Models;

namespace SmartX.API.Services
{
    public class TelemetrySimulationService
    {
        private readonly TelemetryService _telemetryService;
        private readonly Random _random = new();

        public TelemetrySimulationService(TelemetryService telemetryService)
        {
            _telemetryService = telemetryService;
        }

        public LargeScaleSimulationResult SimulateTelemetry(int deviceCount)
        {
            if (deviceCount <= 0)
                throw new ArgumentException(
                    "Device count must be greater than zero.");

            int telemetryRecordsGenerated = 0;

            DateTime startTime = DateTime.UtcNow;

            for (int i = 1; i <= deviceCount; i++)
            {
                string deviceIdentifier =
                    $"MOCK-MCU-{i:D4}";

                // Soil moisture telemetry
                var soilMoisture = new TelemetryPacket<float>
                {
                    DeviceIdentifier = deviceIdentifier,
                    SensorName = "Soil Moisture Sensor",
                    Unit = "%",
                    Timestamp = DateTime.UtcNow,
                    Value = (float)(_random.NextDouble() * 40 + 30)
                };

                _telemetryService.AddFloatTelemetry(soilMoisture);
                telemetryRecordsGenerated++;

                // Power consumption telemetry
                var powerConsumption = new TelemetryPacket<int>
                {
                    DeviceIdentifier = deviceIdentifier,
                    SensorName = "Power Consumption Sensor",
                    Unit = "W",
                    Timestamp = DateTime.UtcNow,
                    Value = _random.Next(500, 5000)
                };

                _telemetryService.AddIntegerTelemetry(powerConsumption);
                telemetryRecordsGenerated++;

                // Valve state telemetry
                var valveState = new TelemetryPacket<bool>
                {
                    DeviceIdentifier = deviceIdentifier,
                    SensorName = "Valve Sensor",
                    Unit = "State",
                    Timestamp = DateTime.UtcNow,
                    Value = _random.Next(0, 2) == 1
                };

                _telemetryService.AddBooleanTelemetry(valveState);
                telemetryRecordsGenerated++;
            }

            DateTime endTime = DateTime.UtcNow;

            double durationMilliseconds =
                (endTime - startTime).TotalMilliseconds;

            return new LargeScaleSimulationResult
            {
                DevicesSimulated = deviceCount,
                TelemetryRecordsGenerated = telemetryRecordsGenerated,
                DurationMilliseconds = durationMilliseconds,
                AverageRecordsPerDevice =
                    (double)telemetryRecordsGenerated / deviceCount
            };
        }
    }
}