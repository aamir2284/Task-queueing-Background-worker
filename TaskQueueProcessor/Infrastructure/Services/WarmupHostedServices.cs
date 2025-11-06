using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskQueueProcessor.Application.Interfaces;

namespace TaskQueueProcessor.Infrastructure.Services
{
    /// <summary>
    /// A lightweight background service that keeps the application warm and responsive.
    /// It periodically runs small internal tasks (like simple database or queue checks)
    /// to prevent cold starts or idle shutdowns in the hosting environment.
    /// The service is safe, low-overhead, and fully configurable for enablement and interval timing.
    /// </summary>

    public class WarmupOptions
    {
        public bool Enabled { get; set; } = true;
        public int IntervalSeconds { get; set; } = 30;
    }

    public class WarmupHostedService : BackgroundService
    {
        private readonly ILogger<WarmupHostedService> _logger;
        private readonly IBackgroundTaskQueue _queue;
        private readonly WarmupOptions _options;

        public WarmupHostedService(ILogger<WarmupHostedService> logger,
            IBackgroundTaskQueue queue,
            IOptions<WarmupOptions> options)
        {
            _logger = logger;
            _queue = queue;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("WarmupHostedService disabled by configuration.");
                return;
            }

            _logger.LogInformation("WarmupHostedService started. Interval: {sec}s", _options.IntervalSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // A very lightweight operation that keeps the runtime busy and ensures
                    // background services are active: get approximate queue count.
                    var count = await _queue.GetApproximateCountAsync(stoppingToken);
                    _logger.LogDebug("Warmup tick - queue approx count: {count}", count);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Warmup tick failed");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_options.IntervalSeconds), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            _logger.LogInformation("WarmupHostedService stopping.");
        }
    }
}