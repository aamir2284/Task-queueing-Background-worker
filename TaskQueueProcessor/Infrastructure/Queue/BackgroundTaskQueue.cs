using System.Collections.Concurrent;
using TaskQueueProcessor.Application.Interfaces;
using TaskQueueProcessor.Domain.Entities;

namespace TaskQueueProcessor.Infrastructure.Queue
{
    /// <summary>
    /// Simple in-memory queue using ConcurrentQueue with a SemaphoreSlim signal.
    /// DequeueAsync waits asynchronously until an item is available (no busy-wait).
    /// Suitable for demo/single-instance. Replace with an external queue for multi-instance scaling.
    /// </summary>
    public class BackgroundTaskQueue : IBackgroundTaskQueue, IDisposable
    {
        private readonly ConcurrentQueue<TaskItem> _queue = new();
        private readonly SemaphoreSlim _signal = new(0);

        public Task EnqueueAsync(TaskItem item, CancellationToken cancellationToken = default)
        {
            _queue.Enqueue(item);
            // release the signal to indicate availability; use try/catch for safety
            try { _signal.Release(); } catch { /* ignored */ }
            return Task.CompletedTask;
        }

        public async Task<TaskItem?> DequeueAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _signal.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }

            if (_queue.TryDequeue(out var item)) return item;
            return null;
        }

        public Task<int> GetApproximateCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_queue.Count);

        public void Dispose()
        {
            _signal.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
