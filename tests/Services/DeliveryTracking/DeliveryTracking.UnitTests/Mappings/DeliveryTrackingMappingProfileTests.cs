using AutoMapper;
using DeliveryTracking.Application.DTOs;
using DeliveryTracking.Application.Mappings;
using DeliveryTracking.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DeliveryTracking.UnitTests.Mappings;

public class DeliveryTrackingMappingProfileTests
{
    private readonly IMapper _mapper;
    private readonly IConfigurationProvider _configuration;

    public DeliveryTrackingMappingProfileTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAutoMapper(cfg =>
        {
            cfg.AddProfile<DeliveryTrackingMappingProfile>();
        });

        var sp = services.BuildServiceProvider();
        _mapper = sp.GetRequiredService<IMapper>();
        _configuration = sp.GetRequiredService<IConfigurationProvider>();
    }

    [Fact]
    public void AutoMapper_Configuration_HarusValid()
    {
        // Assert: Memastikan semua mapping rule valid, tidak ada properti unmapped atau mismatch konstruktor
        _configuration.AssertConfigurationIsValid();
    }

    [Fact]
    public void Map_DeliveryProgressKeDeliveryProgressDto_HarusBerhasilDanSesuai()
    {
        // Arrange
        var progressId = Guid.NewGuid();
        var entity = DeliveryProgress.Create(
            progressId,
            "DO-TEST-001",
            "B1234XYZ",
            "3171012345670001",
            "PT Vendor Logistik",
            "LPCD01");

        // Act
        var dto = _mapper.Map<DeliveryProgressDto>(entity);

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be(progressId);
        dto.DeliveryNo.Should().Be("DO-TEST-001");
        dto.PlatNo.Should().Be("B1234XYZ");
        dto.NoKtp.Should().Be("3171012345670001");
        dto.VendorName.Should().Be("PT Vendor Logistik");
        dto.Lpcd.Should().Be("LPCD01");
        dto.CreatedAt.Should().BeNull();
        dto.CreatedBy.Should().BeNull();
        dto.LastModified.Should().BeNull();
        dto.LastModifiedBy.Should().BeNull();
    }

    [Fact]
    public void Map_DeliveryTrackingSessionKeDeliveryTrackingSessionDto_HarusBerhasilDanSesuai()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var vendorId = Guid.NewGuid();
        var startedAt = DateTime.UtcNow.AddHours(-2);
        var arrivedAt = DateTime.UtcNow.AddMinutes(-30);
        var completedAt = DateTime.UtcNow;

        var session = new DeliveryTrackingSession
        {
            Id = sessionId,
            DeliveryNo = "DO-TOYOTA-888",
            Lpcd = "LPCD99",
            VendorId = vendorId,
            MatchKey = "PlatNo",
            MatchValue = "B 9999 TYY",
            CleanMatchValue = "B9999TYY",
            DriverKtp = "3201000000000002",
            Status = "COMPLETED",
            StartedAt = startedAt,
            ArrivedAt = arrivedAt,
            CompletedAt = completedAt,
            DestinationGeofenceId = Guid.NewGuid(),
            CompletedBy = "admin@edcl.co.id",
            CompletionSource = "AUTO_GEOFENCE",
            IsDeleted = false
        };

        // Act
        var dto = _mapper.Map<DeliveryTrackingSessionDto>(session);

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be(sessionId);
        dto.DeliveryNo.Should().Be("DO-TOYOTA-888");
        dto.Lpcd.Should().Be("LPCD99");
        dto.VendorId.Should().Be(vendorId);
        dto.VendorName.Should().BeNull(); // Belum di-enrich oleh lookup (sesuai mapping contract)
        dto.Key.Should().Be("PlatNo");
        dto.Val.Should().Be("B 9999 TYY");
        dto.CleanValue.Should().Be("B9999TYY");
        dto.DriverKtp.Should().Be("3201000000000002");
        dto.Status.Should().Be("COMPLETED");
        dto.StartedAt.Should().Be(startedAt);
        dto.ArrivedAt.Should().Be(arrivedAt);
        dto.CompletedAt.Should().Be(completedAt);
    }

    [Fact]
    public void Map_DaftarDeliveryTrackingSessionKeDaftarDto_HarusBerhasilDanSesuai()
    {
        // Arrange
        var sessions = new List<DeliveryTrackingSession>
        {
            DeliveryTrackingSession.Create("DO-1", "LPCD1", "PlatNo", "B1111AA", "B1111AA"),
            DeliveryTrackingSession.Create("DO-2", "LPCD2", "PlatNo", "B2222BB", "B2222BB")
        };

        // Act
        var dtos = _mapper.Map<IReadOnlyList<DeliveryTrackingSessionDto>>(sessions);

        // Assert
        dtos.Should().NotBeNull();
        dtos.Should().HaveCount(2);
        dtos[0].DeliveryNo.Should().Be("DO-1");
        dtos[0].CleanValue.Should().Be("B1111AA");
        dtos[1].DeliveryNo.Should().Be("DO-2");
        dtos[1].CleanValue.Should().Be("B2222BB");
    }
}
