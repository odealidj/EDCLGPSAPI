using Delivery.Data.Repositories.IRepositories;
using Delivery.Delivery.Dtos;
using Delivery.Delivery.Models;

namespace Delivery.Delivery.Features.UpsertDeliveryProgress;

public record CreateDeliveryProgressCommand(DeliveryProgressDto DeliveryProgress)
    : ICommand<CreateDeliveryProgressResult>;
public record CreateDeliveryProgressResult(Guid Id);
    
public class CreateDeliveryProgressCommandValidator : AbstractValidator<CreateDeliveryProgressCommand>
{
    public CreateDeliveryProgressCommandValidator()
    {
        RuleFor(x => x.DeliveryProgress.DeliveryNo).NotEmpty().WithMessage("Delivery Number is required");
        ////RuleFor(x => x.GeofenceMaster.LpcdId).NotEmpty().WithMessage("LPCD is required");
    }
}


 
internal class CreateDeliveryProgressHandler(IDeliveryRepository repository)
    : ICommandHandler<CreateDeliveryProgressCommand, CreateDeliveryProgressResult>
{
    public async Task<CreateDeliveryProgressResult> Handle(CreateDeliveryProgressCommand command, CancellationToken cancellationToken)
    {
        var deliveryProgress = CreateNewDeliveryProgress(command.DeliveryProgress);        
        
        var id =  await repository.UpsertDeliveryProgressAsync(deliveryProgress, cancellationToken);
        return new CreateDeliveryProgressResult(id);
    }

    private DeliveryProgress CreateNewDeliveryProgress(DeliveryProgressDto deliveryProgress)
    {
        // create new DeliveryProgress
        var newDeliveryProgress = DeliveryProgress.Create(
            Guid.NewGuid(),
            deliveryProgress.DeliveryNo,
            deliveryProgress.PlatNo,
            deliveryProgress.NoKtp,
            deliveryProgress.VendorName,
            deliveryProgress.Lpcd
        );

        return newDeliveryProgress;
    }
}
