using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using TaskQueueProcessor.Application.Services;
using TaskQueueProcessor.Domain.Entities;
using TaskQueueProcessor.Infrastructure.Data;

namespace TaskQueueProcessor.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TasksController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly TaskProcessorService _taskProcessor;
        private readonly ILogger<TasksController> _logger;

        public TasksController(AppDbContext db, TaskProcessorService taskProcessor, ILogger<TasksController> logger)
        {
            _db = db;
            _taskProcessor = taskProcessor;
            _logger = logger;
        }

        /// <summary>
        /// Simulate an "external" DB update by adding a record directly to DB.
        /// In a real scenario, external apps would INSERT directly; this endpoint is for demo.
        /// </summary>
        [HttpPost("simulate-db-update")]
        public async Task<IActionResult> SimulateDbUpdate([FromBody] SimulateRequest req)
        {
            var item = new TaskItem
            {
                Payload = req.Payload ?? "demo-payload"
                // Enqueued will be false - polling producer will detect it
            };

            _db.TaskItems.Add(item);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Simulated external DB update. New TaskItem Id={id}", item.Id);
            return CreatedAtAction(nameof(GetStatus), new { id = item.Id }, item);
        }

        [HttpGet("status/{id:int}")]
        public async Task<IActionResult> GetStatus(int id)
        {
            var t = await _db.TaskItems.FindAsync(id);
            if (t == null) return NotFound();
            return Ok(t);
        }

        [HttpGet("pending")]
        public async Task<IActionResult> Pending()
        {
            var pendingDb = await _db.TaskItems.CountAsync(t => !t.Processed && !t.Failed);
            return Ok(new { dbPending = pendingDb });
        }

        public class SimulateRequest { public string? Payload { get; set; } }
    }
}
