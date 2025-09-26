namespace VendorMaster.UnitTests.UseCases;

public class DeleteVendorHandlerTests
{
    private readonly IVendorMasterRepository _repository = Substitute.For<IVendorMasterRepository>();
    private readonly ILogger<DeleteVendorHandler> _logger = Substitute.For<ILogger<DeleteVendorHandler>>();
    private readonly DeleteVendorHandler _handler;

    public DeleteVendorHandlerTests()
    {
        _handler = new DeleteVendorHandler(_repository, _logger);
    }

    [Fact]
    public async Task Handle_VendorAda_HarusHapusDanReturnSuccess()
    {
        // arrange
        var id = Guid.NewGuid();
        _repository.DeleteVendorAsync(id, Arg.Any<CancellationToken>())
            .Returns(true);

        var command = new DeleteVendorCommand(id);

        // act
        var result = await _handler.Handle(command, CancellationToken.None);

        // assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();

        await _repository.Received(1).DeleteVendorAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_VendorTidakAda_HarusLemparVendorNotFoundException()
    {
        // arrange
        var id = Guid.NewGuid();
        _repository.DeleteVendorAsync(id, Arg.Any<CancellationToken>())
            .Returns(false);

        var command = new DeleteVendorCommand(id);

        // act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // assert
        await act.Should().ThrowAsync<VendorNotFoundException>();
    }
}
