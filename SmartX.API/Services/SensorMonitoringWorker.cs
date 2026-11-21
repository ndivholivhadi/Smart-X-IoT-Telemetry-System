namespace SmartX.API.Services
{
    public class SensorMonitoringWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public SensorMonitoringWorker(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _serviceProvider.CreateScope();

                var monitoringService =
                    scope.ServiceProvider
                        .GetRequiredService<SensorMonitoringService>();

                monitoringService.CheckSensorConnections();

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}