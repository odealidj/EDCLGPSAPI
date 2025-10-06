using AutoMapper;
using DeliveryTracking.Application.DTOs;
using DeliveryTracking.Application.Mappings;
using DeliveryTracking.Application.Ports;
using DeliveryTracking.Application.UseCases.GetActiveDeliverySessions;
using DeliveryTracking.Application.UseCases.GetDeliveryProgress;
using DeliveryTracking.Application.UseCases.GetDeliverySession;
using DeliveryTracking.Domain.Entities;
using DeliveryTracking.Domain.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeliveryTracking.UnitTests.UseCases;

public class DeliveryQueryHandlersTests
{
    private readonly IMapper _mapper;
    private readonly Mock<IDeliveryTrackingRepository> _repositoryMock = new();
    private readonly Mock<ILogger<GetDeliveryProgressQueryHandler>> _progressLoggerMock = new();
    private readonly Mock<ILogger<GetDeliverySessionQueryHandler>> _sessionLoggerMock = new();
    private readonly Mock<ILogger<GetActiveDeliverySessionsQueryHandler>> _activeSessionsLoggerMock = new();

    public DeliveryQueryHandlersTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAutoMapper(cfg =>
        {
            cfg.AddProfile<DeliveryTrackingMappingProfile>();
        });
        var sp = services.BuildServiceProvider();
        _mapper = sp.GetRequiredService<IMapper>();
    }

    [Fact]
    public async Task GetDeliveryProgressQuery_DataAda_HarusReturnDtoDenganAutoMapper()
    {
        // Arrange
        var deliveryNo = "DO-PROGRESS-001";
        var entity = DeliveryProgress.Create(
            Guid.NewGuid(),
            deliveryNo,
            "B1234ABC",
            "3171012345670001",
            "PT Vendor Logistik",
            "LPCD01");

        _repositoryMock
            .Setup(r => r.GetDeliveryProgressByDeliveryNoAsync(deliveryNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var handler = new GetDeliveryProgressQueryHandler(_repositoryMock.Object, _mapper, _progressLoggerMock.Object);

        // Act
        var result = await handler.Handle(new GetDeliveryProgressQuery(deliveryNo), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.DeliveryNo.Should().Be(deliveryNo);
        result.PlatNo.Should().Be("B1234ABC");
        result.VendorName.Should().Be("PT Vendor Logistik");
        result.Lpcd.Should().Be("LPCD01");
    }

    [Fact]
    public async Task GetDeliveryProgressQuery_DataTidakAda_HarusThrowNotFoundException()
    {
        // Arrange
        var deliveryNo = "DO-NOT-FOUND";
        _repositoryMock
            .Setup(r => r.GetDeliveryProgressByDeliveryNoAsync(deliveryNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeliveryProgress?)null);

        var handler = new GetDeliveryProgressQueryHandler(_repositoryMock.Object, _mapper, _progressLoggerMock.Object);

        // Act
        var act = () => handler.Handle(new GetDeliveryProgressQuery(deliveryNo), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DeliveryProgressNotFoundException>();
    }

    [Fact]
    public async Task GetDeliverySessionQuery_DataAdaDenganLpcd_HarusReturnDtoDenganEnrichedVendorName()
    {
        // Arrange
        var deliveryNo = "DO-SESSION-001";
        var lpcd = "LPCD88";
        var session = DeliveryTrackingSession.Create(deliveryNo, lpcd, "PlatNo", "B 5555 XYZ", "B5555XYZ");

        _repositoryMock
            .Setup(r => r.GetDeliverySessionByDeliveryNoAsync(deliveryNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _repositoryMock
            .Setup(r => r.GetVendorByLpcdAsync(lpcd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VendorByLpcdLookupResult(Guid.NewGuid(), "PT Global Express", lpcd));

        var handler = new GetDeliverySessionQueryHandler(_repositoryMock.Object, _mapper, _sessionLoggerMock.Object);

        // Act
        var result = await handler.Handle(new GetDeliverySessionQuery(deliveryNo), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.DeliveryNo.Should().Be(deliveryNo);
        result.Lpcd.Should().Be(lpcd);
        result.CleanValue.Should().Be("B5555XYZ");
        result.VendorName.Should().Be("PT Global Express");
    }

    [Fact]
    public async Task GetDeliverySessionQuery_DataTidakAda_HarusThrowNotFoundException()
    {
        // Arrange
        var deliveryNo = "DO-MISSING";
        _repositoryMock
            .Setup(r => r.GetDeliverySessionByDeliveryNoAsync(deliveryNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeliveryTrackingSession?)null);

        var handler = new GetDeliverySessionQueryHandler(_repositoryMock.Object, _mapper, _sessionLoggerMock.Object);

        // Act
        var act = () => handler.Handle(new GetDeliverySessionQuery(deliveryNo), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DeliveryProgressNotFoundException>();
    }

    [Fact]
    public async Task GetActiveDeliverySessionsQuery_HarusReturnDaftarDtoTerkonversi()
    {
        // Arrange
        var sessions = new List<DeliveryTrackingSession>
        {
            DeliveryTrackingSession.Create("DO-A1", "LPCD1", "PlatNo", "B111AA", "B111AA"),
            DeliveryTrackingSession.Create("DO-A2", "LPCD2", "PlatNo", "B222BB", "B222BB")
        };

        _repositoryMock
            .Setup(r => r.GetActiveDeliverySessionsAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        var handler = new GetActiveDeliverySessionsQueryHandler(_repositoryMock.Object, _mapper, _activeSessionsLoggerMock.Object);

        // Act
        var result = await handler.Handle(new GetActiveDeliverySessionsQuery(50), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result[0].DeliveryNo.Should().Be("DO-A1");
        result[1].DeliveryNo.Should().Be("DO-A2");
    }
}
