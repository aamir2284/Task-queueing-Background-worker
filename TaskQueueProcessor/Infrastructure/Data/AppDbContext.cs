using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskQueueProcessor.Domain.Entities;

namespace TaskQueueProcessor.Infrastructure.Data
{
    /// <summary>
    /// Application database context representing persistence for queued tasks.
    /// </summary>
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        // Database table for queued work items
        public DbSet<TaskItem> TaskItems { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TaskItem>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Payload)
                      .IsRequired()
                      .HasMaxLength(500);

                entity.Property(e => e.Enqueued)
                      .HasDefaultValue(false);

                entity.Property(e => e.Processed)
                      .HasDefaultValue(false);

                entity.Property(e => e.Failed)
                      .HasDefaultValue(false);

                entity.Property(e => e.AttemptCount)
                      .HasDefaultValue(0);

                entity.Property(e => e.CreatedAt)
                      .HasDefaultValueSql("GETUTCDATE()");
            });
        }
    }
}
