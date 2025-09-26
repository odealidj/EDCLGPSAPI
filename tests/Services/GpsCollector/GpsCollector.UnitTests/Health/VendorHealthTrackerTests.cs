using FluentAssertions;
using GpsCollector.Application.Ports;
using GpsCollector.Domain.Models;
using GpsCollector.Infrastructure.Adapters.Alerting;
using GpsCollector.Infrastructure.Adapters.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GpsCollector.UnitTests.Health;

public class VendorHealthTrackerTests
{
    private readonly Mock<IVendorAlertNotifier> _alertNotifier = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactory = new();
    private readonly Mock<IServiceScope> _scope = new();
    private readonly Mock<IServiceProvider> _serviceProvider = new();

    private readonly VendorHealthTracker _tracker;

    public VendorHealthTrackerTests()
    {
        _serviceProvider.Setup(sp => sp.GetService(typeof(IVendorAlertNotifier)))
            .Returns(_alertNotifier.Object);
        _scope.Setup(s => s.ServiceProvider).Returns(_serviceProvider.Object);
        _scopeFactory.Setup(f => f.CreateScope()).Returns(_scope.Object);

        // Threshold = 3
        _tracker = new VendorHealthTracker(
            _scopeFactory.Object,
            NullLogger<VendorHealthTracker>.Instance,
            failureThreshold: 3
        );
    }

    [Fact]
    public async Task RecordFailureAsync_BelowThreshold_ShouldNotTriggerAlert()
    {
        var vendor = new VendorConfig { Id = Guid.NewGuid(), VendorName = "EasyGo" };
        var endpoint = new VendorEndpointConfig { Id = Guid.NewGuid(), BaseUrl = "https://api.easygo.id" };

        await _tracker.RecordFailureAsync(vendor, endpoint, 500, "Internal Server Error", CancellationToken.None);
        await _tracker.RecordFailureAsync(vendor, endpoint, 502, "Bad Gateway", CancellationToken.None);

        _tracker.GetConsecutiveFailures(vendor.Id, endpoint.Id).Should().Be(2);
        _tracker.IsAlertActive(vendor.Id, endpoint.Id).Should().BeFalse();

        _alertNotifier.Verify(n => n.NotifyAlertTriggeredAsync(It.IsAny<VendorAlertEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecordFailureAsync_ReachingThreshold_ShouldTriggerAlert()
    {
        var vendor = new VendorConfig { Id = Guid.NewGuid(), VendorName = "Cartrack" };
        var endpoint = new VendorEndpointConfig { Id = Guid.NewGuid(), BaseUrl = "https://api.cartrack.com" };

        await _tracker.RecordFailureAsync(vendor, endpoint, 504, "Gateway Timeout", CancellationToken.None);
        await _tracker.RecordFailureAsync(vendor, endpoint, 504, "Gateway Timeout", CancellationToken.None);
        await _tracker.RecordFailureAsync(vendor, endpoint, 504, "Gateway Timeout", CancellationToken.None);

        _tracker.GetConsecutiveFailures(vendor.Id, endpoint.Id).Should().Be(3);
        _tracker.IsAlertActive(vendor.Id, endpoint.Id).Should().BeTrue();

        _alertNotifier.Verify(n => n.NotifyAlertTriggeredAsync(It.Is<VendorAlertEvent>(e =>
            e.VendorId == vendor.Id &&
            e.VendorName == "Cartrack" &&
            e.ConsecutiveFailures == 3 &&
            e.LastStatusCode == 504 &&
            e.AlertType == "ALERT_TRIGGERED"
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordFailureAsync_AboveThreshold_ShouldNotTriggerDuplicateAlert()
    {
        var vendor = new VendorConfig { Id = Guid.NewGuid(), VendorName = "McEasy" };
        var endpoint = new VendorEndpointConfig { Id = Guid.NewGuid(), BaseUrl = "https://api.mceasy.id" };

        // 3 failures trigger alert
        await _tracker.RecordFailureAsync(vendor, endpoint, 500, "Error", CancellationToken.None);
        await _tracker.RecordFailureAsync(vendor, endpoint, 500, "Error", CancellationToken.None);
        await _tracker.RecordFailureAsync(vendor, endpoint, 500, "Error", CancellationToken.None);

        // 4th failure
        await _tracker.RecordFailureAsync(vendor, endpoint, 500, "Error", CancellationToken.None);

        _tracker.GetConsecutiveFailures(vendor.Id, endpoint.Id).Should().Be(4);
        _tracker.IsAlertActive(vendor.Id, endpoint.Id).Should().BeTrue();

        // Still only 1 alert triggered!
        _alertNotifier.Verify(n => n.NotifyAlertTriggeredAsync(It.IsAny<VendorAlertEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordSuccessAsync_AfterAlertActive_ShouldTriggerRecoveryAlertAndReset()
    {
        var vendor = new VendorConfig { Id = Guid.NewGuid(), VendorName = "Puninar" };
        var endpoint = new VendorEndpointConfig { Id = Guid.NewGuid(), BaseUrl = "https://api.puninar.com" };

        // Trigger alert
        await _tracker.RecordFailureAsync(vendor, endpoint, 503, "Service Unavailable", CancellationToken.None);
        await _tracker.RecordFailureAsync(vendor, endpoint, 503, "Service Unavailable", CancellationToken.None);
        await _tracker.RecordFailureAsync(vendor, endpoint, 503, "Service Unavailable", CancellationToken.None);

        _tracker.IsAlertActive(vendor.Id, endpoint.Id).Should().BeTrue();

        // Now success occurs
        await _tracker.RecordSuccessAsync(vendor, endpoint, CancellationToken.None);

        _tracker.GetConsecutiveFailures(vendor.Id, endpoint.Id).Should().Be(0);
        _tracker.IsAlertActive(vendor.Id, endpoint.Id).Should().BeFalse();

        _alertNotifier.Verify(n => n.NotifyAlertResolvedAsync(It.Is<VendorAlertEvent>(e =>
            e.VendorId == vendor.Id &&
            e.VendorName == "Puninar" &&
            e.AlertType == "ALERT_RESOLVED" &&
            e.Downtime != null
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DatabaseVendorAlertNotifier_NotifyAlertTriggered_ShouldWriteToApiLog()
    {
        var apiLogRepo = new Mock<IGpsApiLogRepository>();
        var notifier = new DatabaseVendorAlertNotifier(apiLogRepo.Object, NullLogger<DatabaseVendorAlertNotifier>.Instance);

        var alertEvent = new VendorAlertEvent
        {
            VendorId = Guid.NewGuid(),
            VendorName = "EasyGo",
            EndpointUrl = "https://api.easygo.id/track",
            ConsecutiveFailures = 3,
            LastStatusCode = 502,
            LastErrorMessage = "Bad Gateway",
            AlertType = "ALERT_TRIGGERED",
            Timestamp = DateTime.UtcNow
        };

        await notifier.NotifyAlertTriggeredAsync(alertEvent, CancellationToken.None);

        apiLogRepo.Verify(r => r.LogApiCallAsync(It.Is<GpsApiLogRecord>(l =>
            l.Status == "ALERT_TRIGGERED" &&
            l.Username == "EasyGo" &&
            l.FunctionName == "https://api.easygo.id/track" &&
            l.ErrorMessage!.Contains("3x berturut-turut") &&
            l.Parameter!.Contains("ALERT_TRIGGERED")
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DatabaseVendorAlertNotifier_NotifyAlertResolved_ShouldWriteToApiLog()
    {
        var apiLogRepo = new Mock<IGpsApiLogRepository>();
        var notifier = new DatabaseVendorAlertNotifier(apiLogRepo.Object, NullLogger<DatabaseVendorAlertNotifier>.Instance);

        var alertEvent = new VendorAlertEvent
        {
            VendorId = Guid.NewGuid(),
            VendorName = "EasyGo",
            EndpointUrl = "https://api.easygo.id/track",
            ConsecutiveFailures = 3,
            LastStatusCode = 200,
            AlertType = "ALERT_RESOLVED",
            Timestamp = DateTime.UtcNow,
            Downtime = TimeSpan.FromSeconds(125)
        };

        await notifier.NotifyAlertResolvedAsync(alertEvent, CancellationToken.None);

        apiLogRepo.Verify(r => r.LogApiCallAsync(It.Is<GpsApiLogRecord>(l =>
            l.Status == "ALERT_RESOLVED" &&
            l.Username == "EasyGo" &&
            l.ErrorMessage!.Contains("telah pulih normal") &&
            l.Parameter!.Contains("125")
        ), It.IsAny<CancellationToken>()), Times.Once);
    }
}
