using Microsoft.Extensions.Logging;
using TaskQueueProcessor.Application.Interfaces;
using TaskQueueProcessor.Domain.Entities;

namespace TaskQueueProcessor.Application.Services
{
    /// <summary>
    /// Application-level service that accepts tasks from controllers or producer and delegates to queue.
    /// Keeps domain logic separated from infrastructure.
    /// </summary>
    public class TaskProcessorService
    {
        private readonly IBackgroundTaskQueue _queue;
        private readonly ILogger<TaskProcessorService> _logger;

        public TaskProcessorService(IBackgroundTaskQueue queue, ILogger<TaskProcessorService> logger)
        {
            _queue = queue;
            _logger = logger;
        }

        /// <summary>
        /// Adds a TaskItem to DB (handled by controller in Api) and enqueues it.
        /// </summary>
        public async Task EnqueueAsync(TaskItem item, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("TaskProcessorService: Enqueueing task payload: {payload}", item.Payload);
            await _queue.EnqueueAsync(item, cancellationToken);
        }
    }
}
