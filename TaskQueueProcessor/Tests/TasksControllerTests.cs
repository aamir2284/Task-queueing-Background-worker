using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TaskQueueProcessor.Api.Controllers;
using TaskQueueProcessor.Domain.Entities;
using TaskQueueProcessor.Infrastructure.Data;
using Xunit;

namespace TaskQueueProcessor.Tests
{
    public class TasksControllerTests
    {
        private static AppDbContext CreateInMemoryDb(string dbName)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            var ctx = new AppDbContext(options);
            // Ensure created
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();
            return ctx;
        }

        [Fact]
        public async Task SimulateDbUpdate_CreatesItem_ReturnsCreated()
        {
            using var db = CreateInMemoryDb(nameof(SimulateDbUpdate_CreatesItem_ReturnsCreated));
            var controller = new TasksController(db, null!, NullLogger<TasksController>.Instance);

            var req = new TasksController.SimulateRequest { Payload = "test-payload" };
            var result = await controller.SimulateDbUpdate(req);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(nameof(TasksController.GetStatus), created.ActionName);

            var createdItem = Assert.IsType<TaskItem>(created.Value);
            Assert.False(string.IsNullOrEmpty(createdItem.Payload));
            Assert.True(createdItem.Id > 0);

            // verify persisted
            var dbItem = await db.TaskItems.FindAsync(createdItem.Id);
            Assert.NotNull(dbItem);
            Assert.Equal("test-payload", dbItem!.Payload);
        }

        [Fact]
        public async Task GetStatus_ReturnsOk_WhenFound()
        {
            using var db = CreateInMemoryDb(nameof(GetStatus_ReturnsOk_WhenFound));
            var item = new TaskItem { Payload = "p" };
            db.TaskItems.Add(item);
            await db.SaveChangesAsync();

            var controller = new TasksController(db, null!, NullLogger<TasksController>.Instance);

            var res = await controller.GetStatus(item.Id);
            var ok = Assert.IsType<OkObjectResult>(res);
            var returned = Assert.IsType<TaskItem>(ok.Value);
            Assert.Equal(item.Id, returned.Id);
        }

        [Fact]
        public async Task GetStatus_ReturnsNotFound_WhenMissing()
        {
            using var db = CreateInMemoryDb(nameof(GetStatus_ReturnsNotFound_WhenMissing));
            var controller = new TasksController(db, null!, NullLogger<TasksController>.Instance);

            var res = await controller.GetStatus(9999);
            Assert.IsType<NotFoundResult>(res);
        }

        [Fact]
        public async Task Pending_ReturnsCorrectCount()
        {
            using var db = CreateInMemoryDb(nameof(Pending_ReturnsCorrectCount));
            // 5 pending (not processed, not failed), 2 processed, 1 failed
            for (int i = 0; i < 5; i++) db.TaskItems.Add(new TaskItem { Payload = $"p{i}", Processed = false, Failed = false });
            for (int i = 0; i < 2; i++) db.TaskItems.Add(new TaskItem { Payload = $"proc{i}", Processed = true, Failed = false });
            db.TaskItems.Add(new TaskItem { Payload = "f", Processed = false, Failed = true });
            await db.SaveChangesAsync();

            var controller = new TasksController(db, null!, NullLogger<TasksController>.Instance);
            var res = await controller.Pending();
            var ok = Assert.IsType<OkObjectResult>(res);
            // anonymous object: extract dbPending property via reflection
            var value = ok.Value!;
            var prop = value.GetType().GetProperty("dbPending");
            Assert.NotNull(prop);
            var count = (int)prop!.GetValue(value)!;
            Assert.Equal(5, count);
        }
    }
}