using System;

namespace TaskQueueProcessor.Domain.Entities
{
    /// <summary>
    /// Domain entity representing a queued work item that maps to a database table.
    /// </summary>
    public class TaskItem
    {
        public int Id { get; set; } // DB primary key
        public string Payload { get; set; } = string.Empty; // JSON or simple string describing work
        public bool Enqueued { get; set; } = false; // set true when enqueued to in-memory or external queue
        public bool Processed { get; set; } = false; // set true once successfully processed
        public bool Failed { get; set; } = false; // set true after repeated failures (dead-letter)
        public int AttemptCount { get; set; } = 0; // attempts to process
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }
        public DateTime? FailedAt { get; set; }
    }
}
