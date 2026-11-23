namespace SmartX.Client.Models
{
    public class Sensor
    {
        public Guid Id { get; set; }

        public string DeviceIdentifier { get; set; } = string.Empty;

        public string DeploymentLocation { get; set; } = string.Empty;

        public string Zone { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public DateTime LastSeen { get; set; }
    }
}