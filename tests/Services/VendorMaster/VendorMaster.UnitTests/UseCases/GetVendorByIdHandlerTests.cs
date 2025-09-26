namespace VendorMaster.UnitTests.UseCases;

public class GetVendorByIdHandlerTests
{
    private readonly IVendorMasterRepository _repository = Substitute.For<IVendorMasterRepository>();
    private readonly GetVendorByIdHandler _handler;

    public GetVendorByIdHandlerTests()
    {
        _handler = new GetVendorByIdHandler(_repository);
    }

    [Fact]
    public async Task Handle_VendorAda_HarusReturnVendorDtoLengkap()
    {
        // arrange
        var id = Guid.NewGuid();
        var vendor = GpsVendor.Create(id, "VND_TOYOTA", "Toyota Telematics GPS");
        vendor.AddEndpoint(
            Guid.NewGuid(), 
            EndpointType.TELEMETRY_LAST_POS.ToString(), 
            "https://api.toyota.com/gps",
            VendorDefaults.DefaultHttpMethod,
            VendorDefaults.DefaultContentType,
            null, null, null, null, null);
        vendor.AddLpcd(Guid.NewGuid(), "B9999XYZ");

        _repository.GetVendorByIdAsync(id, true, Arg.Any<CancellationToken>())
            .Returns(vendor);

        var query = new GetVendorByIdQuery(id);

        // act
        var result = await _handler.Handle(query, CancellationToken.None);

        // assert
        result.Should().NotBeNull();
        result.Vendor.Id.Should().Be(id);
        result.Vendor.VendorCode.Should().Be("VND_TOYOTA");
        result.Vendor.Endpoints.Should().HaveCount(1);
        result.Vendor.Lpcds.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_VendorTidakAda_HarusLemparVendorNotFoundException()
    {
        // arrange
        var id = Guid.NewGuid();
        _repository.GetVendorByIdAsync(id, true, Arg.Any<CancellationToken>())
            .Returns((GpsVendor?)null);

        var query = new GetVendorByIdQuery(id);

        // act
        var act = async () => await _handler.Handle(query, CancellationToken.None);

        // assert
        await act.Should().ThrowAsync<VendorNotFoundException>();
    }
}
