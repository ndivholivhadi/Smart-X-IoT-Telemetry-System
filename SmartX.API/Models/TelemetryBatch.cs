using System.Text.Json.Serialization;

namespace SmartX.API.Models
{
    public class TelemetryBatch
    {
        public string DeviceIdentifier { get; set; } = string.Empty;

        // Raw telemetry values before conversion to List<T>
        public double[][] RawTelemetry { get; set; } = Array.Empty<double[]>();

        [JsonRequired]
        public int BatchSize { get; set; }
    }
}