using System.Text.Json.Serialization;

namespace SmartX.API.Models
{
    public class Sensor
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string DeviceIdentifier { get; set; } = string.Empty;

        // Unique hardware address (e.g. 24:6F:28:AB:CD:EF). Generated when not supplied.
        public string MacAddress { get; set; } = string.Empty;

        public string DeploymentLocation { get; set; } = string.Empty;

        public string Zone { get; set; } = string.Empty;

        [JsonRequired]
        public SensorCategory Category { get; set; }

        public SensorStatus Status { get; set; } = SensorStatus.Online;

        public DateTime LastSeen { get; set; } = DateTime.UtcNow;

        public bool TelemetryEnabled { get; set; } = true;
    }
}