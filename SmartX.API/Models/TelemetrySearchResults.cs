namespace SmartX.API.Models
{
    public class TelemetrySearchResult<T>
    {
        public string DeviceIdentifier { get; set; } = string.Empty;

        public List<TelemetryPacket<T>> Results { get; set; } = new();

        public string SearchAlgorithm { get; set; } = string.Empty;

        public long SearchTimeMicroseconds { get; set; }

        public int ResultsFound { get; set; }
    }
}