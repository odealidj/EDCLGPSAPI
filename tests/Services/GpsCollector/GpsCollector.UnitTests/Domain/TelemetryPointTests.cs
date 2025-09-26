using FluentAssertions;
using GpsCollector.Domain.Models;

namespace GpsCollector.UnitTests.Domain;

public class TelemetryPointTests
{
    [Fact]
    public void SetMappedField_PlatNo_ShouldNormalizeUppercaseAndTrim()
    {
        var point = new TelemetryPoint();

        TelemetryPoint.SetMappedField(point, "PlatNo", "  b 1234 cd  ");

        point.PlatNo.Should().Be("B 1234 CD");
    }

    [Fact]
    public void SetMappedField_CoordinatesAndSpeed_ShouldParseCorrectly()
    {
        var point = new TelemetryPoint();

        TelemetryPoint.SetMappedField(point, "X", "106.8456");
        TelemetryPoint.SetMappedField(point, "Y", "-6.2088");
        TelemetryPoint.SetMappedField(point, "Speed", "65.5");
        TelemetryPoint.SetMappedField(point, "Course", 180);

        point.X.Should().Be(106.8456m);
        point.Y.Should().Be(-6.2088m);
        point.Speed.Should().Be(65.5m);
        point.Course.Should().Be(180m);
    }

    [Fact]
    public void SetMappedField_Datetime_ShouldParseUtc()
    {
        var point = new TelemetryPoint();

        TelemetryPoint.SetMappedField(point, "Datetime", "2026-09-27T10:30:00Z");

        point.Datetime.Year.Should().Be(2026);
        point.Datetime.Month.Should().Be(9);
        point.Datetime.Day.Should().Be(27);
        point.Datetime.Hour.Should().Be(10);
        point.Datetime.Minute.Should().Be(30);
    }
}
