
namespace SmartX.API.Models
{
    public class TelemetrySearchRecord
    {
        public string DeviceIdentifier { get; set; } = "";
        public string TelemetryCategory { get; set; } = "";
        public DateTime SearchedAt { get; set; } = DateTime.UtcNow;
        public int ResultsFound { get; set; }
        public long SearchTimeMicroseconds { get; set; }
    }
}
