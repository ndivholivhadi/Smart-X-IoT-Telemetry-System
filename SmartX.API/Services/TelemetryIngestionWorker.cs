namespace SmartX.API.Services
{
    /// <summary>
    /// The consumer for the telemetry queues. Producers (the simulator and the
    /// POST endpoints) only enqueue; this worker takes packets off the queues
    /// in FIFO order, stores them and runs the anomaly checks.
    /// </summary>
    public class TelemetryIngestionWorker : BackgroundService
    {
        private readonly TelemetryService _telemetryService;
        private readonly ILogger<TelemetryIngestionWorker> _logger;

        public TelemetryIngestionWorker(
            TelemetryService telemetryService,
            ILogger<TelemetryIngestionWorker> logger)
        {
            _telemetryService = telemetryService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                int processed = 0;

                try
                {
                    processed = _telemetryService.ProcessPendingTelemetry();
                }
                catch (Exception ex)
                {
                    // One bad packet must not stop ingestion for everything else.
                    _logger.LogError(ex, "Telemetry ingestion failed.");
                }

                // Busy: loop again straight away. Idle: wait a moment.
                if (processed == 0)
                    await Task.Delay(TimeSpan.FromMilliseconds(100), stoppingToken);
            }
        }
    }
}
