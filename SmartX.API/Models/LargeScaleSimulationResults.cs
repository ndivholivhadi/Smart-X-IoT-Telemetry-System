namespace SmartX.API.Models
{
    public class LargeScaleSimulationResult
    {
        public int DevicesSimulated { get; set; }

        public int TelemetryRecordsGenerated { get; set; }

        public double DurationMilliseconds { get; set; }

        public double AverageRecordsPerDevice { get; set; }
    }
}