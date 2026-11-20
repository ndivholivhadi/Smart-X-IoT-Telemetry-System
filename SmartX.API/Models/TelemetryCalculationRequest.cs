namespace SmartX.API.Models
{
    public class TelemetryCalculationRequest
    {
        public TelemetryReading First { get; set; } = new();
        public TelemetryReading Second { get; set; } = new();
    }
}