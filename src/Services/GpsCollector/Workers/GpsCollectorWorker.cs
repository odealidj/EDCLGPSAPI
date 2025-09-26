using GpsCollector.Application.Ports;
using GpsCollector.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.LeaderElection;

namespace GpsCollector.Workers;

// Background worker berkala untuk pengumpulan telemetri armada GPS
public sealed class GpsCollectorWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILeaderElection? _leaderElection;
    private readonly ICollectorMetrics? _metrics;
    private readonly ILogger<GpsCollectorWorker> _logger;

    public GpsCollectorWorker(
        IServiceProvider serviceProvider,
        ILogger<GpsCollectorWorker> logger,
        ILeaderElection? leaderElection = null,
        ICollectorMetrics? metrics = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _leaderElection = leaderElection;
        _metrics = metrics;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("GpsCollectorWorker is starting.");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // 1. Leader Election: hanya 1 instance yang aktif melakukan polling
                if (_leaderElection != null)
                {
                    var isLeader = await _leaderElection.TryAcquireAsync(TimeSpan.FromSeconds(90), stoppingToken);
                    _metrics?.SetLeaderStatus(isLeader);

                    if (!isLeader)
                    {
                        // Standby instance menunggu 30 detik sebelum mencoba kembali
                        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                        continue;
                    }
                }
                else
                {
                    _metrics?.SetLeaderStatus(true);
                }

                int delayMinutes = 1;

                // 2. Eksekusi pengumpulan data dalam service scope
                using (var scope = _serviceProvider.CreateScope())
                {
                    var orchestrator = scope.ServiceProvider.GetRequiredService<IGpsCollectorOrchestrator>();
                    var configRepo = scope.ServiceProvider.GetRequiredService<IVendorConfigRepository>();

                    delayMinutes = await configRepo.GetPollingDelayMinutesAsync(stoppingToken);

                    var results = await orchestrator.CollectAllVendorsAsync(stoppingToken);
                    _logger.LogInformation("Completed vendor poll cycle. Processed {Count} vendors.", results.Count);
                }

                // 3. Perpanjang lease leader election
                if (_leaderElection != null)
                {
                    await _leaderElection.RenewAsync(TimeSpan.FromSeconds(90), stoppingToken);
                }

                // 4. Istirahat sesuai interval jeda
                await Task.Delay(TimeSpan.FromMinutes(delayMinutes), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("GpsCollectorWorker execution cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in GpsCollectorWorker: {Message}", ex.Message);
        }
        finally
        {
            _metrics?.SetLeaderStatus(false);
            if (_leaderElection != null)
            {
                try
                {
                    await _leaderElection.ReleaseAsync(CancellationToken.None);
                }
                catch
                {
                    // Abaikan kesalahan saat release di akhir
                }
            }
            _logger.LogInformation("GpsCollectorWorker has stopped.");
        }
    }
}
