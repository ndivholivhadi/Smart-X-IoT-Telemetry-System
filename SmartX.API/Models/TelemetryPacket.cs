namespace SmartX.API.Models
{
    public class TelemetryPacket<T>
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string DeviceIdentifier { get; set; } = string.Empty;

        public string SensorName { get; set; } = string.Empty;

        public string Unit { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public T Value { get; set; } = default!;
    }
}