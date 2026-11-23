namespace SmartX.Client.Models
{
    public class Alerts
    {
        public Guid Id { get; set; }

        public string DeviceIdentifier { get; set; } = string.Empty;

        public string SensorName { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string Severity { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; }

        public bool IsResolved { get; set; }
    }
}