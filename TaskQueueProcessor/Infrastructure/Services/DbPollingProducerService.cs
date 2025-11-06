using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskQueueProcessor.Application.Interfaces;
using TaskQueueProcessor.Domain.Entities;
using TaskQueueProcessor.Infrastructure.Data; // Add this using directive

namespace TaskQueueProcessor.Infrastructure.Services
{
    /// <summary>
    /// Periodically checks the database for new records inserted by external applications
    /// and adds them to the processing queue.
    /// This method is database-agnostic, easy to set up, and ideal for moderate workloads.
    /// For larger-scale systems, consider using Change Data Capture (CDC) or Service Broker instead.
    /// </summary>

    public class PollerOptions
    {
        public int PollingIntervalSeconds { get; set; } = 5;
        public int BatchSize { get; set; } = 50;
    }

    public class DbPollingProducerService : BackgroundService
    {
        private readonly ILogger<DbPollingProducerService> _logger;
        private readonly IServiceProvider _services;
        private readonly IBackgroundTaskQueue _queue;
        private readonly PollerOptions _options;

        public DbPollingProducerService(ILogger<DbPollingProducerService> logger,
            IServiceProvider services,
            IBackgroundTaskQueue queue,
            IOptions<PollerOptions> options)
        {
            _logger = logger;
            _services = services;
            _queue = queue;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("DB Polling Producer started. Polling every {sec}s, batch={batch}",
                _options.PollingIntervalSeconds, _options.BatchSize);

            // Immediate initial poll to reduce cold-start latency
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _services.CreateScope();
                    // FIX: Use the correct namespace and type for AppDbContext
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();


                    // Find new tasks written to DB by external apps which are not yet enqueued
                    var taskItems = db.Set<TaskItem>();

                    var newItems = await taskItems
                        .Where(t => !t.Enqueued && !t.Processed && !t.Failed)
                        .OrderBy(t => t.CreatedAt)
                        .Take(_options.BatchSize)
                        .ToListAsync(stoppingToken);

                    if (newItems.Any())
                    {
                        _logger.LogInformation("Found {count} new DB task(s) to enqueue", newItems.Count);
                        foreach (var t in newItems)
                        {
                            // mark as enqueued in DB to avoid duplicates
                            t.Enqueued = true;
                            try
                            {
                                await _queue.EnqueueAsync(t, stoppingToken);
                            }
                            catch (Exception qex)
                            {
                                _logger.LogError(qex, "Failed to enqueue task {id}; leaving Enqueued flag as false", t.Id);
                                // revert flag on failure in-memory (will be saved after loop)
                                t.Enqueued = false;
                            }
                        }

                        await db.SaveChangesAsync(stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while polling DB");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_options.PollingIntervalSeconds), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            _logger.LogInformation("DB Polling Producer stopping.");
        }
    }
}
