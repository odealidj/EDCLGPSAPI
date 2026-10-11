using System.Diagnostics.Metrics;
using FluentAssertions;
using GpsCollector.Metrics;
using Moq;

namespace GpsCollector.UnitTests.Metrics;

public class CollectorMetricsTests
{
    private class TestMeterFactory : IMeterFactory
    {
        public Meter Create(MeterOptions options) => new(options.Name, options.Version);
        public void Dispose() { }
    }

    [Fact]
    public void CollectorMetrics_Methods_HarusBekerjaTanpaException()
    {
        // arrange
        var factory = new TestMeterFactory();
        var metrics = new CollectorMetrics(factory);

        // act & assert
        var act = () =>
        {
            metrics.RecordVendorPoll("Vendor A", true, 120.5);
            metrics.RecordVendorPoll("Vendor B", false, 500.0);
            metrics.RecordRecordsInserted(10);
            metrics.RecordMongoInserted(5);
            metrics.SetLeaderStatus(true);
            metrics.SetLeaderStatus(false);
            metrics.UpdateLastCollectionTimestamp();
        };

        act.Should().NotThrow();
    }
}
