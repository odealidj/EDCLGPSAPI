using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace GpsCollector.Metrics;

public interface ICollectorMetrics
{
    void RecordVendorPoll(string vendorName, bool success, double durationMs);
    void RecordRecordsInserted(int count);
    void RecordMongoInserted(int count);
    void SetLeaderStatus(bool isLeader);
    void UpdateLastCollectionTimestamp();
}

public sealed class CollectorMetrics : ICollectorMetrics
{
    public const string MeterName = "edcl.gps.collector";
    private readonly Meter _meter;
    private readonly Counter<int> _vendorPolls;
    private readonly Histogram<double> _pollDurationMs;
    private readonly Counter<int> _recordsInserted;
    private readonly Counter<int> _mongoInserted;
    private int _isLeader;
    private long _lastCollectionTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public CollectorMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);
        _vendorPolls = _meter.CreateCounter<int>("gps.vendor.polls.total", description: "Total vendor polling attempts");
        _pollDurationMs = _meter.CreateHistogram<double>("gps.vendor.poll.duration.ms", unit: "ms", description: "Vendor polling duration");
        _recordsInserted = _meter.CreateCounter<int>("gps.records.inserted.total", description: "Total GPS records inserted into PostgreSQL");
        _mongoInserted = _meter.CreateCounter<int>("gps.mongo.docs.inserted.total", description: "Total raw documents inserted into MongoDB");

        _meter.CreateObservableGauge("gps.leader.status", () => _isLeader, description: "1 if current instance is active leader, 0 if standby");
        _meter.CreateObservableGauge("gps.last.collection.timestamp", () => _lastCollectionTimestamp, description: "Unix timestamp of last successful GPS collection");
    }

    public void RecordVendorPoll(string vendorName, bool success, double durationMs)
    {
        var tags = new TagList
        {
            { "vendorName", vendorName },
            { "success", success ? "true" : "false" }
        };
        _vendorPolls.Add(1, tags);
        _pollDurationMs.Record(durationMs, tags);
    }

    public void RecordRecordsInserted(int count)
    {
        _recordsInserted.Add(count);
    }

    public void RecordMongoInserted(int count)
    {
        _mongoInserted.Add(count);
    }

    public void SetLeaderStatus(bool isLeader)
    {
        Interlocked.Exchange(ref _isLeader, isLeader ? 1 : 0);
    }

    public void UpdateLastCollectionTimestamp()
    {
        Interlocked.Exchange(ref _lastCollectionTimestamp, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }
}
