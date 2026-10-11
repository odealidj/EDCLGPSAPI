using FluentAssertions;
using GpsCollector.Application.Ports;
using GpsCollector.Metrics;
using GpsCollector.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shared.Infrastructure.LeaderElection;

namespace GpsCollector.UnitTests.Workers;

public class GpsCollectorWorkerTests
{
    [Fact]
    public async Task ExecuteAsync_KetikaTokenSudahDibatalkan_HarusBerhentiDenganAman()
    {
        // arrange
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();

        var envMock = new Mock<IHostEnvironment>();
        envMock.SetupGet(e => e.EnvironmentName).Returns(Environments.Development);

        var worker = new GpsCollectorWorker(sp, envMock.Object, NullLogger<GpsCollectorWorker>.Instance);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // batalkan sebelum mulai

        // act
        await worker.StartAsync(cts.Token);
        await worker.StopAsync(CancellationToken.None);

        // assert: tidak boleh crash
        worker.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_KetikaBukanLeader_HarusSetMetricsFalseDanSkip()
    {
        // arrange
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();

        var envMock = new Mock<IHostEnvironment>();
        envMock.SetupGet(e => e.EnvironmentName).Returns(Environments.Development);

        var leaderMock = new Mock<ILeaderElection>();
        using var cts = new CancellationTokenSource();

        leaderMock.Setup(l => l.TryAcquireAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel()) // batalkan loop setelah iterasi pertama
            .ReturnsAsync(false);

        var metricsMock = new Mock<ICollectorMetrics>();

        var worker = new GpsCollectorWorker(
            sp,
            envMock.Object,
            NullLogger<GpsCollectorWorker>.Instance,
            leaderMock.Object,
            metricsMock.Object);

        // act
        await worker.StartAsync(cts.Token);
        await Task.Delay(50);
        await worker.StopAsync(CancellationToken.None);

        // assert
        metricsMock.Verify(m => m.SetLeaderStatus(false), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_KetikaLeader_HarusJalankanScopeDanRenew()
    {
        // arrange
        var orchestratorMock = new Mock<IGpsCollectorOrchestrator>();
        orchestratorMock.Setup(o => o.CollectAllVendorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var configRepoMock = new Mock<IVendorConfigRepository>();
        configRepoMock.Setup(c => c.GetPollingDelayMinutesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var services = new ServiceCollection();
        services.AddScoped(_ => orchestratorMock.Object);
        services.AddScoped(_ => configRepoMock.Object);
        var sp = services.BuildServiceProvider();

        var envMock = new Mock<IHostEnvironment>();
        envMock.SetupGet(e => e.EnvironmentName).Returns(Environments.Development);

        using var cts = new CancellationTokenSource();
        var leaderMock = new Mock<ILeaderElection>();
        leaderMock.Setup(l => l.TryAcquireAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        leaderMock.Setup(l => l.RenewAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel()) // batalkan setelah renew
            .ReturnsAsync(true);

        var metricsMock = new Mock<ICollectorMetrics>();

        var worker = new GpsCollectorWorker(
            sp,
            envMock.Object,
            NullLogger<GpsCollectorWorker>.Instance,
            leaderMock.Object,
            metricsMock.Object);

        // act
        await worker.StartAsync(cts.Token);
        await Task.Delay(50);
        await worker.StopAsync(CancellationToken.None);

        // assert
        orchestratorMock.Verify(o => o.CollectAllVendorsAsync(It.IsAny<CancellationToken>()), Times.Once);
        leaderMock.Verify(l => l.RenewAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_KetikaTanpaLeaderElection_HarusTetapJalankanScope()
    {
        // arrange
        var orchestratorMock = new Mock<IGpsCollectorOrchestrator>();
        using var cts = new CancellationTokenSource();

        orchestratorMock.Setup(o => o.CollectAllVendorsAsync(It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())
            .ReturnsAsync([]);

        var configRepoMock = new Mock<IVendorConfigRepository>();
        configRepoMock.Setup(c => c.GetPollingDelayMinutesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var services = new ServiceCollection();
        services.AddScoped(_ => orchestratorMock.Object);
        services.AddScoped(_ => configRepoMock.Object);
        var sp = services.BuildServiceProvider();

        var envMock = new Mock<IHostEnvironment>();
        envMock.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);

        var metricsMock = new Mock<ICollectorMetrics>();

        var worker = new GpsCollectorWorker(
            sp,
            envMock.Object,
            NullLogger<GpsCollectorWorker>.Instance,
            leaderElection: null,
            metrics: metricsMock.Object);

        // act
        await worker.StartAsync(cts.Token);
        await Task.Delay(50);
        await worker.StopAsync(CancellationToken.None);

        // assert
        metricsMock.Verify(m => m.SetLeaderStatus(true), Times.AtLeastOnce);
        orchestratorMock.Verify(o => o.CollectAllVendorsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_KetikaReleaseAsyncError_HarusDitanganiDenganAman()
    {
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();

        var envMock = new Mock<IHostEnvironment>();
        envMock.SetupGet(e => e.EnvironmentName).Returns(Environments.Development);

        var leaderMock = new Mock<ILeaderElection>();
        leaderMock.Setup(l => l.ReleaseAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Release failed"));

        var worker = new GpsCollectorWorker(
            sp,
            envMock.Object,
            NullLogger<GpsCollectorWorker>.Instance,
            leaderElection: leaderMock.Object);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await worker.StartAsync(cts.Token);
        var act = async () => await worker.StopAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}

