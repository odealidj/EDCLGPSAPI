using DeliveryTracking.Application.DTOs;
using DeliveryTracking.Application.Ports;
using DeliveryTracking.Domain.Exceptions;
using Shared.Contracts.CQRS;

namespace DeliveryTracking.Application.UseCases.DeleteDeliveryProgress;

public record DeleteDeliveryProgressCommand(string DeliveryNo)
    : ICommand<DeleteDeliveryProgressResult>;

public class DeleteDeliveryProgressCommandValidator : AbstractValidator<DeleteDeliveryProgressCommand>
{
    public DeleteDeliveryProgressCommandValidator()
    {
        RuleFor(x => x.DeliveryNo)
            .NotEmpty().WithMessage("Nomor DO / delivery wajib diisi.");
    }
}

public class DeleteDeliveryProgressCommandHandler(
    IDeliveryTrackingRepository repository,
    ILogger<DeleteDeliveryProgressCommandHandler> logger)
    : ICommandHandler<DeleteDeliveryProgressCommand, DeleteDeliveryProgressResult>
{
    public async Task<DeleteDeliveryProgressResult> Handle(
        DeleteDeliveryProgressCommand request,
        CancellationToken cancellationToken)
    {
        var exists = await repository.GetDeliveryProgressByDeliveryNoAsync(request.DeliveryNo, cancellationToken);
        if (exists == null)
        {
            throw new DeliveryProgressNotFoundException(request.DeliveryNo);
        }

        var deleted = await repository.DeleteDeliveryProgressAsync(request.DeliveryNo, cancellationToken);
        logger.LogInformation("Status hapus delivery progress '{DeliveryNo}': {Success}", request.DeliveryNo, deleted);

        return new DeleteDeliveryProgressResult(deleted, request.DeliveryNo);
    }
}
