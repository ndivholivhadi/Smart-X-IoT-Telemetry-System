using SmartX.API.Models;

namespace SmartX.API.Services
{
    public class TelemetryBatchService
    {
        private readonly TelemetryService _telemetryService;

        public TelemetryBatchService(TelemetryService telemetryService)
        {
            _telemetryService = telemetryService;
        }

        public TelemetryBatchProcessingResult ProcessBatch(TelemetryBatch batch)
        {
            if (batch.RawTelemetry == null ||
                batch.RawTelemetry.Length == 0)
            {
                return new TelemetryBatchProcessingResult();
            }

            int processedCount = 0;
            int totalRawValues = 0;

            var batchSizes = new List<int>();

            foreach (double[] telemetryBatch in batch.RawTelemetry)
            {
                List<double> optimizedTelemetry = telemetryBatch.ToList();

                batchSizes.Add(optimizedTelemetry.Count);

                totalRawValues += optimizedTelemetry.Count;

                foreach (double value in optimizedTelemetry)
                {
                    var packet = new TelemetryPacket<float>
                    {
                        DeviceIdentifier = batch.DeviceIdentifier,
                        SensorName = "Soil Moisture Sensor",
                        Unit = "%",
                        Timestamp = DateTime.UtcNow,
                        Value = (float)value
                    };

                    _telemetryService.AddFloatTelemetry(packet);

                    processedCount++;
                }
            }

            return new TelemetryBatchProcessingResult
            {
                DeviceIdentifier = batch.DeviceIdentifier,
                NumberOfBatches = batch.RawTelemetry.Length,
                TotalRawValues = totalRawValues,
                ValuesProcessed = processedCount,
                BatchSizes = batchSizes
            };
        }
    }
}