namespace SmartX.API.Services
{
    public class TelemetrySimulationWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public TelemetrySimulationWorker(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _serviceProvider.CreateScope();

                var simulator = scope.ServiceProvider
                    .GetRequiredService<TelemetrySimulator>();

                simulator.GenerateTelemetry();

                await Task.Delay(
                    TimeSpan.FromSeconds(5),
                    stoppingToken);
            }
        }
    }
}