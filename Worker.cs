using Microsoft.Extensions.Options;

namespace GemService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly WorkerOptions _options;

        public Worker(ILogger<Worker> logger, IOptions<WorkerOptions> options)
        {
            _logger = logger;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker started at: {time}", DateTimeOffset.Now);

            while (!stoppingToken.IsCancellationRequested)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("{Message}: {time}", _options.Message, DateTimeOffset.Now);
                }
                await Task.Delay(_options.DelayMilliseconds, stoppingToken);
            }
            _logger.LogInformation("Worker service stopping at: {time}", DateTimeOffset.Now);
        }
    }
}
