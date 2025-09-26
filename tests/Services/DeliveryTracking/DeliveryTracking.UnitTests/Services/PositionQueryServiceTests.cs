using System.Text.Json;
using DeliveryTracking.Application.DTOs;
using DeliveryTracking.Application.Ports;
using DeliveryTracking.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeliveryTracking.UnitTests.Services;

// unit tests untuk PositionQueryService (redis-first + db fallback) dengan Moq
public class PositionQueryServiceTests
{
    private readonly Mock<IDistributedCache> _cacheMock = new();
    private readonly Mock<IDeliveryTrackingRepository> _repositoryMock = new();
    private readonly Mock<ILogger<PositionQueryService>> _loggerMock = new();
    private readonly PositionQueryService _service;

    public PositionQueryServiceTests()
    {
        _service = new PositionQueryService(_cacheMock.Object, _repositoryMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetLastPositionByPlatNo_WhenPlateEmpty_ReturnsNull()
    {
        // Act
        var result = await _service.GetLastPositionByPlatNoAsync("");

        // Assert
        result.Should().BeNull();
        _repositoryMock.Verify(r => r.GetLastPositionByPlatNoFromDbAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetLastPositionByPlatNo_WhenRedisCacheHit_ReturnsRedisDataWithoutDbCall()
    {
        // Arrange
        const string platNo = "B1234XYZ";
        var cachedDto = new
        {
            Id = Guid.NewGuid(),
            PlatNo = "B1234XYZ",
            DeviceId = "DEV-001",
            VendorName = "PT Vendor Tracker",
            Datetime = DateTime.UtcNow,
            X = 106.827m,
            Y = -6.175m,
            Speed = 40.0m,
            Course = 90.0m,
            StreetName = "Jl. Thamrin",
            CachedAt = DateTime.UtcNow
        };
        var jsonBytes = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cachedDto));
        _cacheMock.Setup(c => c.GetAsync($"gps:last:{platNo}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(jsonBytes);

        // Act
        var result = await _service.GetLastPositionByPlatNoAsync(platNo);

        // Assert
        result.Should().NotBeNull();
        result!.PlatNo.Should().Be("B1234XYZ");
        result.DataSource.Should().Be("Redis");
        _repositoryMock.Verify(r => r.GetLastPositionByPlatNoFromDbAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetLastPositionByPlatNo_WhenRedisCacheMiss_FallsBackToPostgresDb()
    {
        // Arrange
        const string platNo = "B1234XYZ";
        _cacheMock.Setup(c => c.GetAsync($"gps:last:{platNo}", It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        var dbPosition = new LastPositionDto
        {
            Id = Guid.NewGuid(),
            PlatNo = "B1234XYZ",
            VendorName = "PT Vendor Tracker",
            DataSource = "PostgreSQL"
        };
        _repositoryMock.Setup(r => r.GetLastPositionByPlatNoFromDbAsync(platNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbPosition);

        // Act
        var result = await _service.GetLastPositionByPlatNoAsync(platNo);

        // Assert
        result.Should().NotBeNull();
        result!.PlatNo.Should().Be("B1234XYZ");
        result.DataSource.Should().Be("PostgreSQL");
        _repositoryMock.Verify(r => r.GetLastPositionByPlatNoFromDbAsync(platNo, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueryPositions_WithMultiFilters_CallsDatabaseDirectly()
    {
        // Arrange
        var request = new LastPositionRequest(
            GpsVendorId: Guid.NewGuid(),
            PlatNo: "B1234XYZ",
            PageIndex: 1,
            PageSize: 10
        );

        var dbResponse = new PaginatedPositionResponse(
            PageIndex: 1,
            PageSize: 10,
            Count: 1,
            Data: [new LastPositionDto { PlatNo = "B1234XYZ", DataSource = "PostgreSQL" }]
        );

        _repositoryMock.Setup(r => r.QueryPositionsFromDbAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbResponse);

        // Act
        var result = await _service.QueryPositionsAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(1);
        _repositoryMock.Verify(r => r.QueryPositionsFromDbAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }
}
