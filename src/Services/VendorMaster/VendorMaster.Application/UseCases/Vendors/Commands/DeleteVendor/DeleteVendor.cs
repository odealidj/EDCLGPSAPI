namespace VendorMaster.Application.UseCases.Vendors.Commands.DeleteVendor;

// request & result delete vendor
public record DeleteVendorCommand(Guid Id) : ICommand<DeleteVendorResult>;
public record DeleteVendorResult(bool IsSuccess);

// validasi input delete vendor
public class DeleteVendorValidator : AbstractValidator<DeleteVendorCommand>
{
    public DeleteVendorValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Id vendor wajib diisi.");
    }
}

// handler use case hapus vendor
public class DeleteVendorHandler(
    IVendorMasterRepository repository,
    ILogger<DeleteVendorHandler> logger) : ICommandHandler<DeleteVendorCommand, DeleteVendorResult>
{
    public async Task<DeleteVendorResult> Handle(DeleteVendorCommand command, CancellationToken cancellationToken)
    {
        var success = await repository.DeleteVendorAsync(command.Id, cancellationToken);
        if (!success)
        {
            throw new VendorNotFoundException(command.Id);
        }

        logger.LogInformation("Berhasil menghapus vendor dengan Id: {Id}", command.Id);
        return new DeleteVendorResult(true);
    }
}
