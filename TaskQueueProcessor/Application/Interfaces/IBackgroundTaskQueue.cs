using System;
using System.Threading;
using System.Threading.Tasks;
using TaskQueueProcessor.Domain.Entities;

namespace TaskQueueProcessor.Application.Interfaces
{
    /// <summary>
    /// Abstraction for a pluggable queue. Implementations can be in-memory or external (Azure Storage).
    /// </summary>
    public interface IBackgroundTaskQueue
    {
        Task EnqueueAsync(TaskItem item, CancellationToken cancellationToken = default);
        Task<TaskItem?> DequeueAsync(CancellationToken cancellationToken = default);
        Task<int> GetApproximateCountAsync(CancellationToken cancellationToken = default);
    }
}
