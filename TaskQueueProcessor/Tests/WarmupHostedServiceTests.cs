using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TaskQueueProcessor.Application.Interfaces;
using TaskQueueProcessor.Infrastructure.Services;
using Xunit;

namespace TaskQueueProcessor.Tests
{
    public class WarmupHostedServiceTests
    {
        [Fact]
        public async Task ExecuteAsync_ReturnsImmediately_WhenDisabled()
        {
            var qMock = new Mock<IBackgroundTaskQueue>(MockBehavior.Strict);
            var options = Options.Create(new WarmupOptions { Enabled = false, IntervalSeconds = 1 });

            var svc = new WarmupHostedService(NullLogger<WarmupHostedService>.Instance, qMock.Object, Options.Create(options.Value));

            // Cancellation token that is not canceled, but since disabled, ExecuteAsync should return quickly.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            // Start the service (BackgroundService.StartAsync will call ExecuteAsync)
            await svc.StartAsync(cts.Token);

            // Ensure no calls were made to the queue
            qMock.Verify(q => q.GetApproximateCountAsync(It.IsAny<CancellationToken>()), Times.Never);

            // Stop gracefully
            await svc.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task ExecuteAsync_CallsQueue_WhenEnabled()
        {
            var qMock = new Mock<IBackgroundTaskQueue>();
            // make GetApproximateCountAsync return quickly
            qMock.Setup(q => q.GetApproximateCountAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(42);

            var options = Options.Create(new WarmupOptions { Enabled = true, IntervalSeconds = 1 });
            var svc = new WarmupHostedService(NullLogger<WarmupHostedService>.Instance, qMock.Object, Options.Create(options.Value));

            // Cancel after short time so ExecuteAsync exits
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(350));
            // Start and wait until canceled
            await svc.StartAsync(cts.Token);

            // At least one call expected before cancellation
            qMock.Verify(q => q.GetApproximateCountAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);

            await svc.StopAsync(CancellationToken.None);
        }
    }
}