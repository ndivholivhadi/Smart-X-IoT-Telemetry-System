namespace SmartX.API.Models
{
    public class TelemetryStatistics
    {
        public long TelemetryGenerated { get; set; }
        public long TelemetryTransmitted { get; set; }
        public long TelemetryReceived { get; set; }
        public long TotalRecordsStored { get; set; }

        public DateTime? LastTelemetryReceived { get; set; }

        public int PendingTelemetry { get; set; }
    }
}