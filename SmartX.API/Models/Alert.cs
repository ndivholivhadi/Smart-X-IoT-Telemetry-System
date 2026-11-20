namespace SmartX.API.Models
{
    public class Alert
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string DeviceIdentifier { get; set; } = string.Empty;

        public string SensorName { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string Severity { get; set; } = "Warning";

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public bool IsResolved { get; set; } = false;

        public string Condition { get; set; } = string.Empty;   // e.g. "SoilMoisture.Critical"
    }
}