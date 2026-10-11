namespace VendorMaster.UnitTests.UseCases;

public class CreateVendorHandlerTests
{
    private readonly IVendorMasterRepository _repository = Substitute.For<IVendorMasterRepository>();
    private readonly ILogger<CreateVendorHandler> _logger = Substitute.For<ILogger<CreateVendorHandler>>();
    private readonly CreateVendorHandler _handler;

    public CreateVendorHandlerTests()
    {
        _handler = new CreateVendorHandler(_repository, _logger);
    }

    [Fact]
    public async Task Handle_DataValid_HarusSimpanKeRepoDanReturnIdBaru()
    {
        // arrange
        var dto = new VendorDto
        {
            VendorCode = "VND_TOYOTA",
            VendorName = "Toyota GPS",
            Endpoints =
            [
                new VendorEndpointDto { BaseUrl = "https://api.toyota.com/gps1", EndpointType = EndpointType.TELEMETRY_LAST_POS.ToString() }
            ],
            Lpcds =
            [
                new VendorLpcdDto { Lpcd = "B1234XYZ" }
            ]
        };

        _repository.GetVendorByCodeAsync(dto.VendorCode, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((GpsVendor?)null);

        _repository.CreateVendorAsync(Arg.Any<GpsVendor>(), Arg.Any<CancellationToken>())
            .Returns(args => args.Arg<GpsVendor>());

        var command = new CreateVendorCommand(dto);

        // act
        var result = await _handler.Handle(command, CancellationToken.None);

        // assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();

        await _repository.Received(1).CreateVendorAsync(
            Arg.Is<GpsVendor>(x => x.VendorCode == "VND_TOYOTA"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_VendorCodeUdahAda_HarusLemparVendorConflictException()
    {
        // arrange
        var dto = new VendorDto
        {
            VendorCode = "VND_EXISTING",
            VendorName = "Vendor Sudah Ada",
            Lpcd = "VND_LPCD"
        };

        var existing = GpsVendor.Create(Guid.NewGuid(), "VND_EXISTING", "Existing Vendor");
        _repository.GetVendorByCodeAsync("VND_EXISTING", Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(existing);

        var command = new CreateVendorCommand(dto);

        // act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // assert
        await act.Should().ThrowAsync<VendorConflictException>()
            .WithMessage("*sudah terdaftar*");

        await _repository.DidNotReceive().CreateVendorAsync(Arg.Any<GpsVendor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TanpaLpcd_HarusLemparVendorBadRequestException()
    {
        // arrange
        var dto = new VendorDto
        {
            VendorCode = "VND_TEST",
            VendorName = "Vendor Test"
        };

        var command = new CreateVendorCommand(dto);

        // act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // assert
        await act.Should().ThrowAsync<VendorBadRequestException>()
            .WithMessage("*LPCD*");
    }

    [Fact]
    public async Task Handle_AdaDuplikatLpcd_HarusLemparVendorBadRequestException()
    {
        // arrange
        var dto = new VendorDto
        {
            VendorCode = "VND_TEST",
            VendorName = "Vendor Test",
            Lpcds =
            [
                new VendorLpcdDto { Lpcd = "B1234XYZ" },
                new VendorLpcdDto { Lpcd = "b1234xyz" } // duplikat case-insensitive
            ]
        };

        _repository.GetVendorByCodeAsync(dto.VendorCode, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((GpsVendor?)null);

        var command = new CreateVendorCommand(dto);

        // act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // assert
        await act.Should().ThrowAsync<VendorBadRequestException>()
            .WithMessage("*Duplikat LPCD*");
    }

    [Fact]
    public async Task Handle_LpcdSudahTerdaftarDiVendorLain_HarusLemparVendorConflictException()
    {
        // arrange
        var dto = new VendorDto
        {
            VendorCode = "VND_NEW",
            VendorName = "Vendor Baru",
            Lpcd = "LPCD_EXISTING"
        };

        _repository.GetVendorByCodeAsync(dto.VendorCode, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((GpsVendor?)null);

        _repository.IsLpcdExistsAsync("LPCD_EXISTING", null, Arg.Any<CancellationToken>())
            .Returns(true);

        var command = new CreateVendorCommand(dto);

        // act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // assert
        await act.Should().ThrowAsync<VendorConflictException>()
            .WithMessage("*sudah terdaftar pada vendor lain*");
    }

    [Fact]
    public async Task Handle_AdaDuplikatEndpointBaseUrl_HarusLemparVendorBadRequestException()
    {
        // arrange
        var dto = new VendorDto
        {
            VendorCode = "VND_TEST",
            VendorName = "Vendor Test",
            Lpcd = "VND_LPCD",
            Endpoints =
            [
                new VendorEndpointDto { BaseUrl = "https://api.vendor.com/gps" },
                new VendorEndpointDto { BaseUrl = "https://api.vendor.com/gps" }
            ]
        };

        _repository.GetVendorByCodeAsync(dto.VendorCode, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((GpsVendor?)null);

        var command = new CreateVendorCommand(dto);

        // act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // assert
        await act.Should().ThrowAsync<VendorBadRequestException>()
            .WithMessage("*Duplikat BaseUrl*");
    }

    [Fact]
    public async Task Handle_DenganLpcdCodes_HarusTambahkanKeLpcds()
    {
        var dto = new VendorDto
        {
            VendorCode = "VND_CODES",
            VendorName = "Vendor Codes",
            LpcdCodes = ["LPCD_EXTRA_1", "LPCD_EXTRA_2"]
        };

        _repository.GetVendorByCodeAsync(dto.VendorCode, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((GpsVendor?)null);

        _repository.IsLpcdExistsAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>())
            .Returns(false);

        _repository.CreateVendorAsync(Arg.Any<GpsVendor>(), Arg.Any<CancellationToken>())
            .Returns(args => args.Arg<GpsVendor>());

        var command = new CreateVendorCommand(dto);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();

        await _repository.Received(1).CreateVendorAsync(
            Arg.Is<GpsVendor>(x => x.VendorCode == "VND_CODES"),
            Arg.Any<CancellationToken>());
    }
}


