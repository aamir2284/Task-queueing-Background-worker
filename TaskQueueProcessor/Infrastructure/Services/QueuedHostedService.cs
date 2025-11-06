using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using System;
using TaskQueueProcessor.Application.Interfaces;
using TaskQueueProcessor.Domain.Entities;
using TaskQueueProcessor.Infrastructure.Data;

namespace TaskQueueProcessor.Infrastructure.Services
{
    /// <summary>
    /// A background service that manages and processes queued tasks.
    /// On startup, it reloads any pending tasks from the database,
    /// then continuously processes new tasks from the IBackgroundTaskQueue
    /// with controlled concurrency and automatic retry handling.
    /// Each task’s status is updated in the database as either Processed or Failed.
    /// </summary>

    public class WorkerOptions
    {
        public int MaxDegreeOfParallelism { get; set; } = 4;
        public int RetryCount { get; set; } = 3;
        public int RetryInitialDelayMs { get; set; } = 500;
    }

    public class QueuedHostedService : BackgroundService
    {
        private readonly ILogger<QueuedHostedService> _logger;
        private readonly IBackgroundTaskQueue _queue;
        private readonly IServiceProvider _services;
        private readonly WorkerOptions _options;
        private readonly SemaphoreSlim _semaphore;
        private readonly AsyncRetryPolicy _retryPolicy;

        public QueuedHostedService(ILogger<QueuedHostedService> logger,
            IBackgroundTaskQueue queue,
            IServiceProvider services,
            IOptions<WorkerOptions> options)
        {
            _logger = logger;
            _queue = queue;
            _services = services;
            _options = options.Value;
            _semaphore = new SemaphoreSlim(_options.MaxDegreeOfParallelism);

            // Exponential backoff retry policy using Polly
            _retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(_options.RetryCount,
                    attempt => TimeSpan.FromMilliseconds(_options.RetryInitialDelayMs * Math.Pow(2, attempt - 1)),
                    (ex, ts) => _logger.LogWarning(ex, "Retrying after exception, wait {wait}", ts));
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Worker starting - rehydrating pending tasks from DB to queue...");

            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Rehydrate: enqueue tasks that were persisted but not yet enqueued/processed
            var pending = await db.TaskItems
                .Where(t => !t.Processed && !t.Failed && !t.Enqueued)
                .OrderBy(t => t.CreatedAt)
                .ToListAsync(cancellationToken);

            foreach (var item in pending)
            {
                await _queue.EnqueueAsync(item, cancellationToken);
                item.Enqueued = true;
            }

            await db.SaveChangesAsync(cancellationToken);

            await base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("QueuedHostedService is running.");

            var runningTasks = new List<Task>();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Try to get an item from queue
                    var item = await _queue.DequeueAsync(stoppingToken);
                    if (item == null)
                    {
                        // No item available, small delay (prevents busy wait)
                        await Task.Delay(300, stoppingToken);
                        continue;
                    }

                    await _semaphore.WaitAsync(stoppingToken);

                    // Start processing without blocking loop
                    var task = ProcessItemAsync(item, stoppingToken)
                        .ContinueWith(t => _semaphore.Release());

                    runningTasks.Add(task);

                    // Clean up completed tasks to avoid list growth
                    runningTasks.RemoveAll(t => t.IsCompleted);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // cancellation requested - break loop
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in execution loop");
                    await Task.Delay(1000, stoppingToken);
                }
            }

            _logger.LogInformation("Shutdown requested. Waiting for in-flight tasks to complete...");
            await Task.WhenAll(runningTasks);
            _logger.LogInformation("Worker stopped.");
        }

        /// <summary>
        /// Process a single TaskItem using a scoped DbContext. Retries with poli-based backoff.
        /// </summary>
        private async Task ProcessItemAsync(TaskItem item, CancellationToken cancellationToken)
        {
            try
            {
                await _retryPolicy.ExecuteAsync(async ct =>
                {
                    using var scope = _services.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    // Refresh item from DB to get current attempt count
                    var dbItem = await db.TaskItems.FindAsync(new object[] { item.Id }, ct);
                    if (dbItem == null)
                    {
                        _logger.LogWarning("Task {id} not found in DB; skipping", item.Id);
                        return;
                    }

                    // increment attempt count
                    dbItem.AttemptCount++;
                    await db.SaveChangesAsync(ct);

                    // Simulate real work here. Replace this with actual processing logic.
                    _logger.LogInformation("Processing task {id} payload={payload}", dbItem.Id, dbItem.Payload);
                    await Task.Delay(1000, ct); // simulate some processing time

                    // Save processed state
                    dbItem.Processed = true;
                    dbItem.ProcessedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);

                    _logger.LogInformation("Task {id} processed successfully", dbItem.Id);
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Task {id} failed after retries, marking as failed", item.Id);
                try
                {
                    using var scope = _services.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var dbItem = await db.TaskItems.FindAsync(new object[] { item.Id }, cancellationToken);
                    if (dbItem != null)
                    {
                        dbItem.Failed = true;
                        dbItem.FailedAt = DateTime.UtcNow;
                        await db.SaveChangesAsync(cancellationToken);
                    }
                }
                catch (Exception inner)
                {
                    _logger.LogError(inner, "Error while marking task failed {id}", item.Id);
                }
            }
        }

        public override void Dispose()
        {
            _semaphore.Dispose();
            base.Dispose();
        }
    }
}
