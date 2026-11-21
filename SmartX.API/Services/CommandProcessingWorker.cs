namespace SmartX.API.Services
{
    /// <summary>
    /// The consumer for the command queue. While auto-processing is on it executes
    /// one pending command per interval, oldest first. "Execute Next" in the UI still
    /// works for stepping through the queue by hand (turn auto-processing off first
    /// with POST api/commands/auto-process?enabled=false).
    /// </summary>
    public class CommandProcessingWorker : BackgroundService
    {
        private readonly CommandService _commandService;
        private readonly ILogger<CommandProcessingWorker> _logger;
        private readonly TimeSpan _interval;

        public CommandProcessingWorker(
            CommandService commandService,
            IConfiguration configuration,
            ILogger<CommandProcessingWorker> logger)
        {
            _commandService = commandService;
            _logger = logger;

            var seconds = configuration.GetValue("CommandProcessing:IntervalSeconds", 3);
            _interval = TimeSpan.FromSeconds(Math.Max(1, seconds));

            _commandService.AutoProcess = configuration.GetValue("CommandProcessing:AutoProcess", true);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (_commandService.AutoProcess)
                        _commandService.ExecuteNextCommand();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Command processing failed.");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
    }
}
