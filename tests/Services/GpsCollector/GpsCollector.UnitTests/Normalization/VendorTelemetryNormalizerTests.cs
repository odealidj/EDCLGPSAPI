using FluentAssertions;
using GpsCollector.Domain.Models;
using GpsCollector.Infrastructure.Adapters.Normalization;
using Microsoft.Extensions.Logging.Abstractions;

namespace GpsCollector.UnitTests.Normalization;

public class VendorTelemetryNormalizerTests
{
    private readonly VendorTelemetryNormalizer _normalizer = new(NullLogger<VendorTelemetryNormalizer>.Instance);

    [Fact]
    public void Normalize_IndividualVendor_ShouldParseNestedPropertiesCorrectly()
    {
        var vendorId = Guid.NewGuid();
        var rawJson = """
        {
            "status": "success",
            "data": [
                {
                    "vehicle": { "license": "B 9999 XYZ", "id": "TRUCK-01" },
                    "location": { "lng": 106.8456, "lat": -6.2088 },
                    "metrics": { "speed_kmh": 72.5, "heading": 90 },
                    "timestamp": "2026-09-27T08:00:00Z"
                }
            ]
        }
        """;

        var mappings = new List<VendorFieldMapping>
        {
            new() { GpsVendorId = vendorId, ResponseField = "vehicle.license", MappedField = "PlatNo" },
            new() { GpsVendorId = vendorId, ResponseField = "vehicle.id", MappedField = "DeviceId" },
            new() { GpsVendorId = vendorId, ResponseField = "location.lng", MappedField = "X" },
            new() { GpsVendorId = vendorId, ResponseField = "location.lat", MappedField = "Y" },
            new() { GpsVendorId = vendorId, ResponseField = "metrics.speed_kmh", MappedField = "Speed" },
            new() { GpsVendorId = vendorId, ResponseField = "metrics.heading", MappedField = "Course" },
            new() { GpsVendorId = vendorId, ResponseField = "timestamp", MappedField = "Datetime" }
        };

        var points = _normalizer.Normalize(vendorId, rawJson, "data", mappings, "https://api.vendor.com/track");

        points.Should().HaveCount(1);
        var p = points[0];
        p.PlatNo.Should().Be("B 9999 XYZ");
        p.DeviceId.Should().Be("TRUCK-01");
        p.X.Should().Be(106.8456m);
        p.Y.Should().Be(-6.2088m);
        p.Speed.Should().Be(72.5m);
        p.Course.Should().Be(90m);
        p.Datetime.Year.Should().Be(2026);
    }

    [Fact]
    public void NormalizeCombined_ShouldMergeTwoPayloadsByKey()
    {
        var vendorId = Guid.NewGuid();
        var rawJson1 = """
        {
            "vehicles": [
                { "vin": "VIN001", "plat": "B 1234 CD", "device": "DEV-01" }
            ]
        }
        """;

        var rawJson2 = """
        {
            "vehicles": [
                { "vin": "VIN001", "longitude": 107.1234, "latitude": -6.5678, "speed": 50 }
            ]
        }
        """;

        var mappings = new List<VendorFieldMapping>
        {
            new() { GpsVendorId = vendorId, ResponseField = "plat", MappedField = "PlatNo" },
            new() { GpsVendorId = vendorId, ResponseField = "device", MappedField = "DeviceId" },
            new() { GpsVendorId = vendorId, ResponseField = "longitude", MappedField = "X" },
            new() { GpsVendorId = vendorId, ResponseField = "latitude", MappedField = "Y" },
            new() { GpsVendorId = vendorId, ResponseField = "speed", MappedField = "Speed" }
        };

        var points = _normalizer.NormalizeCombined(
            vendorId,
            new[] { rawJson1, rawJson2 },
            keyPath: "vin",
            dataPath: "vehicles",
            mappings: mappings,
            endpointUrl: "https://api.vendor.com/combined");

        points.Should().HaveCount(1);
        var p = points[0];
        p.PlatNo.Should().Be("B 1234 CD");
        p.DeviceId.Should().Be("DEV-01");
        p.X.Should().Be(107.1234m);
        p.Y.Should().Be(-6.5678m);
        p.Speed.Should().Be(50m);
    }

    [Fact]
    public void ExtractMaxProperty_ShouldFindLargestValue()
    {
        var rawJson = """
        {
            "data": [
                { "id": 101, "name": "A" },
                { "id": 250, "name": "B" },
                { "id": 180, "name": "C" }
            ]
        }
        """;

        var max = _normalizer.ExtractMaxProperty(rawJson, "data", "id");

        max.Should().Be(250);
    }
}
