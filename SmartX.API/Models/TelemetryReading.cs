using System.Text.Json.Serialization;

namespace SmartX.API.Models
{
    public class TelemetryReading
    {
        public string DeviceIdentifier { get; set; } = string.Empty;

        [JsonRequired]
        public double Value { get; set; }
        public string Unit { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Operator overload for aggregating two readings.
        public static TelemetryReading operator +(TelemetryReading first, TelemetryReading second)
        {
            return new TelemetryReading
            {
                DeviceIdentifier = $"{first.DeviceIdentifier} + {second.DeviceIdentifier}",

                Value = first.Value + second.Value,

                Unit = first.Unit,

                Timestamp = DateTime.UtcNow
            };
        }

        // Operator overload for calculating the difference
        // between two sensor readings.
        public static TelemetryReading operator -(TelemetryReading first, TelemetryReading second)
        {
            return new TelemetryReading
            {
                DeviceIdentifier = $"{first.DeviceIdentifier} - {second.DeviceIdentifier}",

                Value = first.Value - second.Value,

                Unit = first.Unit,

                Timestamp = DateTime.UtcNow
            };
        }
    }
}