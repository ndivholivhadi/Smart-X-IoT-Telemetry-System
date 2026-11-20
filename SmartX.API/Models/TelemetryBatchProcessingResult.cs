namespace SmartX.API.Models
{
    public class TelemetryBatchProcessingResult
    {
        public string DeviceIdentifier { get; set; } = string.Empty;

        public int NumberOfBatches { get; set; }

        public int TotalRawValues { get; set; }

        public int ValuesProcessed { get; set; }

        public List<int> BatchSizes { get; set; } = new();
    }
}