namespace VendorMaster.UnitTests.UseCases;

public class UpdateVendorHandlerTests
{
    private readonly IVendorMasterRepository _repository = Substitute.For<IVendorMasterRepository>();
    private readonly ILogger<UpdateVendorHandler> _logger = Substitute.For<ILogger<UpdateVendorHandler>>();
    private readonly UpdateVendorHandler _handler;

    public UpdateVendorHandlerTests()
    {
        _handler = new UpdateVendorHandler(_repository, _logger);
    }

    [Fact]
    public async Task Handle_VendorAda_HarusUpdateDanReturnSuccess()
    {
        // arrange
        var id = Guid.NewGuid();
        var existing = GpsVendor.Create(id, "VND_TOYOTA", "Toyota GPS");

        _repository.GetVendorByIdAsync(id, false, Arg.Any<CancellationToken>())
            .Returns(existing);

        _repository.UpdateVendorAsync(existing, Arg.Any<CancellationToken>())
            .Returns(true);

        var dto = new VendorDto
        {
            Id = id,
            VendorCode = "VND_TOYOTA",
            VendorName = "Toyota GPS Updated",
            ProtocolType = ProtocolType.REST_API.ToString(),
            ProcessingStrategy = ProcessingStrategy.Individual.ToString(),
            Lpcds =
            [
                new VendorLpcdDto { Lpcd = "B1234XYZ" }
            ]
        };

        var command = new UpdateVendorCommand(dto);

        // act
        var result = await _handler.Handle(command, CancellationToken.None);

        // assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        existing.VendorName.Should().Be("Toyota GPS Updated");

        await _repository.Received(1).UpdateVendorAsync(existing, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_VendorTidakAda_HarusLemparVendorNotFoundException()
    {
        // arrange
        var id = Guid.NewGuid();
        _repository.GetVendorByIdAsync(id, false, Arg.Any<CancellationToken>())
            .Returns((GpsVendor?)null);

        var command = new UpdateVendorCommand(new VendorDto { Id = id, VendorName = "Vendor Baru", Lpcd = "AJL" });

        // act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // assert
        await act.Should().ThrowAsync<VendorNotFoundException>();
    }

    [Fact]
    public async Task Handle_VendorCodeDigantiKeKodeYangSudahDipakaiVendorLain_HarusLemparVendorConflictException()
    {
        // arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        var existing = GpsVendor.Create(id1, "VND_ORIGINAL", "Vendor 1");
        var otherVendor = GpsVendor.Create(id2, "VND_TAKEN", "Vendor 2");

        _repository.GetVendorByIdAsync(id1, false, Arg.Any<CancellationToken>())
            .Returns(existing);

        _repository.GetVendorByCodeAsync("VND_TAKEN", Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(otherVendor);

        var dto = new VendorDto
        {
            Id = id1,
            VendorCode = "VND_TAKEN",
            VendorName = "Vendor Mau Rebut Kode",
            Lpcd = "VND1_LPCD"
        };

        var command = new UpdateVendorCommand(dto);

        // act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // assert
        await act.Should().ThrowAsync<VendorConflictException>()
            .WithMessage("*sudah terdaftar pada vendor lain*");
    }

    [Fact]
    public async Task Handle_VendorAda_DenganPropertyLpcdLangsung_HarusUpdateDanReturnSuccess()
    {
        // arrange
        var id = Guid.NewGuid();
        var existing = GpsVendor.Create(id, "VND_HINO", "Hino GPS");

        _repository.GetVendorByIdAsync(id, false, Arg.Any<CancellationToken>())
            .Returns(existing);

        _repository.UpdateVendorAsync(existing, Arg.Any<CancellationToken>())
            .Returns(true);

        var dto = new VendorDto
        {
            Id = id,
            VendorCode = "VND_HINO",
            VendorName = "Hino GPS Updated",
            Lpcd = "HINO123"
        };

        var command = new UpdateVendorCommand(dto);

        // act
        var result = await _handler.Handle(command, CancellationToken.None);

        // assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Lpcd.Should().Be("HINO123");

        await _repository.Received(1).UpdateVendorAsync(existing, Arg.Any<CancellationToken>());
    }
}
