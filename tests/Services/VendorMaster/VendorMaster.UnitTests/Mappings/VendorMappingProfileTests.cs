using AutoMapper;
using VendorMaster.Application.DTOs;
using VendorMaster.Application.Mappings;
using VendorMaster.Domain.Constants;
using VendorMaster.Domain.Entities;
using VendorMaster.Domain.Enums;
using Xunit;
using FluentAssertions;
using System.Text.Json.Nodes;

namespace VendorMaster.UnitTests.Mappings;

public class VendorMappingProfileTests
{
    private readonly IMapper _mapper;
    private readonly IConfigurationProvider _configuration;

    public VendorMappingProfileTests()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddApplicationServices();
        var sp = services.BuildServiceProvider();

        _mapper = sp.GetRequiredService<IMapper>();
        _configuration = sp.GetRequiredService<IConfigurationProvider>();
    }

    [Fact]
    public void AutoMapper_Configuration_HarusValid()
    {
        // Assert
        _configuration.AssertConfigurationIsValid();
    }

    [Fact]
    public void Map_GpsVendorKeVendorDto_HarusBerhasilDanSesuai()
    {
        // Arrange
        var vendorId = Guid.NewGuid();
        var vendor = GpsVendor.Create(
            vendorId,
            "VND_TEST",
            "Vendor Test PT",
            "REST_JSON",
            "Asia/Jakarta",
            true,
            "ARRAY_DIRECT",
            "data.items",
            "records");

        vendor.AddEndpoint(
            Guid.NewGuid(),
            EndpointType.TELEMETRY_LAST_POS.ToString(),
            "https://api.vendortest.com/telemetry",
            VendorDefaults.DefaultHttpMethod,
            VendorDefaults.DefaultContentType,
            new JsonObject { ["Authorization"] = "Bearer token123" },
            null,
            null,
            null,
            null);

        vendor.SetAuth(
            Guid.NewGuid(),
            AuthType.BEARER_TOKEN.ToString(),
            "https://api.vendortest.com/oauth/token",
            "POST",
            "application/json",
            "client_user",
            "secret_pass",
            "access_token",
            "expires_in",
            3600,
            null,
            null,
            null);

        vendor.AddMapping(1, "lat", "latitude", "DECIMAL");
        vendor.AddMapping(2, "lon", "longitude", "DECIMAL");
        vendor.AddLpcd(Guid.NewGuid(), "LPCD01");

        // Act
        var dto = _mapper.Map<VendorDto>(vendor);

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be(vendorId);
        dto.VendorCode.Should().Be("VND_TEST");
        dto.VendorName.Should().Be("Vendor Test PT");
        dto.Lpcd.Should().Be("LPCD01");
        dto.RequiredAuth.Should().BeTrue();
        dto.AuthType.Should().Be(AuthType.BEARER_TOKEN.ToString());
        dto.Username.Should().Be("client_user");
        dto.Password.Should().Be("secret_pass");

        dto.Endpoints.Should().HaveCount(1);
        dto.Endpoints[0].BaseUrl.Should().Be("https://api.vendortest.com/telemetry");
        dto.Endpoints[0].GpsVendorId.Should().Be(vendorId);

        dto.Auth.Should().NotBeNull();
        dto.Auth!.BaseUrl.Should().Be("https://api.vendortest.com/oauth/token");
        dto.Auth.GpsVendorId.Should().Be(vendorId);

        dto.Mappings.Should().HaveCount(2);
        dto.Mappings.Select(m => m.ResponseField).Should().Contain(["lat", "lon"]);

        dto.Lpcds.Should().HaveCount(1);
        dto.Lpcds[0].Lpcd.Should().Be("LPCD01");
        dto.Lpcds[0].GpsVendorId.Should().Be(vendorId);
    }

    [Fact]
    public void Map_GeofenceDestinationKeGeofenceDestinationDto_HarusBerhasil()
    {
        // Arrange
        var geofence = new GeofenceDestination
        {
            Id = Guid.NewGuid(),
            DestinationCode = "PLANT_1",
            DestinationName = "Toyota Karawang Plant 1",
            GeofenceType = "CIRCLE",
            CenterLatitude = -6.3456m,
            CenterLongitude = 107.2345m,
            RadiusMeters = 250,
            IsActive = true
        };

        // Act
        var dto = _mapper.Map<GeofenceDestinationDto>(geofence);

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be(geofence.Id);
        dto.DestinationCode.Should().Be("PLANT_1");
        dto.DestinationName.Should().Be("Toyota Karawang Plant 1");
        dto.CenterLatitude.Should().Be(-6.3456m);
        dto.CenterLongitude.Should().Be(107.2345m);
        dto.RadiusMeters.Should().Be(250);
        dto.IsActive.Should().BeTrue();
    }
}
