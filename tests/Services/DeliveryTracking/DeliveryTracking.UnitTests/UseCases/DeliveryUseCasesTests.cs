namespace DeliveryTracking.UnitTests.UseCases;

public class DeliveryUseCasesTests
{
    private readonly Mock<IDeliveryTrackingRepository> _repositoryMock = new();
    private readonly Mock<ILogger<TrackDeliveryQueryHandler>> _trackLoggerMock = new();
    private readonly Mock<ILogger<UpsertDeliveryProgressCommandHandler>> _upsertLoggerMock = new();
    private readonly Mock<ILogger<BulkInsertDeliveryProgressCommandHandler>> _bulkLoggerMock = new();
    private readonly Mock<ILogger<DeleteDeliveryProgressCommandHandler>> _deleteLoggerMock = new();

    [Fact]
    public async Task TrackDelivery_HarusPanggilRepositoryDanReturnDaftarTitikGps()
    {
        // arrange
        var deliveryNo = "DO-TOYOTA-100";
        var mockPoints = new List<TrackDeliveryPointDto>
        {
            new() { PositionId = 1, DeliveryNo = deliveryNo, X = -6.200000m, Y = 106.816666m, Speed = 60 }
        };

        _repositoryMock
            .Setup(r => r.GetTrackDeliveryAsync(deliveryNo, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockPoints);

        var handler = new TrackDeliveryQueryHandler(_repositoryMock.Object, _trackLoggerMock.Object);

        // act
        var result = await handler.Handle(new TrackDeliveryQuery(deliveryNo, 1), CancellationToken.None);

        // assert
        result.Should().HaveCount(1);
        result[0].DeliveryNo.Should().Be(deliveryNo);
        result[0].Speed.Should().Be(60);
    }

    [Fact]
    public async Task UpsertDeliveryProgress_TanpaVendorTapiAdaLpcd_HarusLookupVendorDanSimpan()
    {
        // arrange
        var deliveryNo = "DO-TOYOTA-200";
        var lpcd = "LPCD-001";
        var expectedVendorId = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.GetVendorByLpcdAsync(lpcd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VendorByLpcdLookupResult(expectedVendorId, "PT Vendor Hebat", lpcd));

        _repositoryMock
            .Setup(r => r.UpsertDeliveryProgressAsync(It.IsAny<DeliveryProgress>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SaveDeliveryProgressResult(Guid.NewGuid(), deliveryNo, true));

        var handler = new UpsertDeliveryProgressCommandHandler(_repositoryMock.Object, _upsertLoggerMock.Object);
        var command = new UpsertDeliveryProgressCommand(new DeliveryProgressDto
        {
            DeliveryNo = deliveryNo,
            PlatNo = "B9999ZZ",
            NoKtp = "31710000000",
            Lpcd = lpcd,
            VendorName = null // sengaja dikosongkan untuk uji lookup otomatis
        });

        // act
        var result = await handler.Handle(command, CancellationToken.None);

        // assert
        result.Should().NotBeNull();
        result.DeliveryNo.Should().Be(deliveryNo);
        result.IsCreated.Should().BeTrue();

        _repositoryMock.Verify(r => r.GetVendorByLpcdAsync(lpcd, It.IsAny<CancellationToken>()), Times.Once);
        _repositoryMock.Verify(r => r.UpsertDeliveryProgressAsync(
            It.Is<DeliveryProgress>(p => p.VendorName == "PT Vendor Hebat"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BulkInsertDeliveryProgress_HarusPanggilRepositoryBinaryCopyDanReturnTotalRows()
    {
        // arrange
        var items = new List<DeliveryProgressDto>
        {
            new() { DeliveryNo = "DO-BATCH-1", PlatNo = "B111", NoKtp = "111" },
            new() { DeliveryNo = "DO-BATCH-2", PlatNo = "B222", NoKtp = "222" }
        };

        _repositoryMock
            .Setup(r => r.BulkInsertDeliveryProgressAsync(It.IsAny<IReadOnlyList<DeliveryProgress>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var handler = new BulkInsertDeliveryProgressCommandHandler(_repositoryMock.Object, _bulkLoggerMock.Object);

        // act
        var result = await handler.Handle(new BulkInsertDeliveryProgressCommand(items), CancellationToken.None);

        // assert
        result.Should().NotBeNull();
        result.RowsInserted.Should().Be(2);
        result.EntityName.Should().Be(nameof(DeliveryProgress));
    }

    [Fact]
    public async Task DeleteDeliveryProgress_DataTidakAda_HarusLemparNotFoundException()
    {
        // arrange
        var deliveryNo = "DO-TIDAK-ADA";
        _repositoryMock
            .Setup(r => r.GetDeliveryProgressByDeliveryNoAsync(deliveryNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeliveryProgress?)null);

        var handler = new DeleteDeliveryProgressCommandHandler(_repositoryMock.Object, _deleteLoggerMock.Object);

        // act
        var act = () => handler.Handle(new DeleteDeliveryProgressCommand(deliveryNo), CancellationToken.None);

        // assert
        await act.Should().ThrowAsync<DeliveryProgressNotFoundException>();
    }

    [Fact]
    public async Task DeleteDeliveryProgress_DataAda_HarusHapusDanReturnTrue()
    {
        // arrange
        var deliveryNo = "DO-ADA";
        _repositoryMock
            .Setup(r => r.GetDeliveryProgressByDeliveryNoAsync(deliveryNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DeliveryProgress.Create(Guid.NewGuid(), deliveryNo, "B123", "999"));

        _repositoryMock
            .Setup(r => r.DeleteDeliveryProgressAsync(deliveryNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new DeleteDeliveryProgressCommandHandler(_repositoryMock.Object, _deleteLoggerMock.Object);

        // act
        var result = await handler.Handle(new DeleteDeliveryProgressCommand(deliveryNo), CancellationToken.None);

        // assert
        result.IsSuccess.Should().BeTrue();
        result.DeliveryNo.Should().Be(deliveryNo);
    }
}
