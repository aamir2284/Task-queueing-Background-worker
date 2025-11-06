using Microsoft.EntityFrameworkCore;
using TaskQueueProcessor.Application.Interfaces;
using TaskQueueProcessor.Application.Services;
using TaskQueueProcessor.Infrastructure.Data;
using TaskQueueProcessor.Infrastructure.Queue;
using TaskQueueProcessor.Infrastructure.Services;

// Create builder
var builder = WebApplication.CreateBuilder(args);

// Add configuration and logging
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// --- EF Core DbContext ---
// Use SQL Server (LocalDB default) - change to production connection string in appsettings
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- Options ---
// Worker options and Poller options bound to their respective sections
builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));
builder.Services.Configure<PollerOptions>(builder.Configuration.GetSection("Poller"));
builder.Services.Configure<WarmupOptions>(builder.Configuration.GetSection("Warmup"));

// --- Application / Infrastructure DI ---
// Queue: In-memory by default; can add AzureStorageTaskQueue implementation separately
builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();

// Add IHttpClientFactory (useful if you later add an external keep-alive)
builder.Services.AddHttpClient();

// Application service
builder.Services.AddScoped<TaskProcessorService>();

// Hosted services:
// - Producer: polls DB for new rows created by external systems and enqueues them.
// - Consumer: dequeues and processes items.
// - Warmup/KeepAlive: lightweight in-process activity to reduce cold starts (configurable).
builder.Services.AddHostedService<DbPollingProducerService>();
builder.Services.AddHostedService<QueuedHostedService>();
builder.Services.AddHostedService<WarmupHostedService>();

// Health checks
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("Database");

// API / Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Ensure database created (for demo)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
