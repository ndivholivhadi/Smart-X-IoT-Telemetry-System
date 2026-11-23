namespace SmartX.Client.Models
{
    public class TelemetryPacket<T>
    {
        public string DeviceIdentifier { get; set; } = string.Empty;

        public string SensorName { get; set; } = string.Empty;

        public T Value { get; set; } = default!;

        public DateTime Timestamp { get; set; }
    }
}