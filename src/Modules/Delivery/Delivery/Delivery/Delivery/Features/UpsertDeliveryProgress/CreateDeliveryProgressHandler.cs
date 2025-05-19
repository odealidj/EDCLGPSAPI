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


 
internal class CreateDeliveryProgressHandler(IDeliveryRepository repository, IDeliveryDapperRepository dapprRepository )
    : ICommandHandler<CreateDeliveryProgressCommand, CreateDeliveryProgressResult>
{
    public async Task<CreateDeliveryProgressResult> Handle(CreateDeliveryProgressCommand command, CancellationToken cancellationToken)
    {

        if (!string.IsNullOrEmpty(command.DeliveryProgress.PlatNo)
            && string.IsNullOrEmpty(command.DeliveryProgress.VendorName) 
            && !string.IsNullOrEmpty(command.DeliveryProgress.Lpcd))
        {
            // check if lpcd is exist
            var gpsVendor = await dapprRepository.GetGpsVendorByLpcdAsync(new InGetGpsVendorByLpcd(command.DeliveryProgress.Lpcd),cancellationToken);
            if (gpsVendor != null) command.DeliveryProgress.VendorName = gpsVendor?.GpsVendorName;

        }
        
        if (!string.IsNullOrEmpty(command.DeliveryProgress.PlatNo)
            && string.IsNullOrEmpty(command.DeliveryProgress.VendorName)
            && string.IsNullOrEmpty(command.DeliveryProgress.Lpcd))
        {
            // check if lpcd is exist
            var gpsVendor = await dapprRepository.GetGpsVendorByPlatNoAsync(new InGetGpsVendorByPlatNo(command.DeliveryProgress.PlatNo),cancellationToken);
            if (gpsVendor != null)
            {
                command.DeliveryProgress.VendorName = gpsVendor.VendorName;
                
                var gpsLpcd = await dapprRepository.GetGpsLpcdByGpsVendorNameAsync(new InGetGpsLpcdByGpsVendorName(gpsVendor.VendorName),cancellationToken);
                if (gpsLpcd != null) command.DeliveryProgress.Lpcd = gpsLpcd.Lpcd;

            }
            

        }
       
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
