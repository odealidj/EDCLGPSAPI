using Delivery.Data;
using Delivery.Delivery.Exceptions;

namespace Delivery.Delivery.Features.DeleteDelievryProgress;


public record DeleteDeliveryProgressCommand(string deliveryNo)
    : ICommand<DeleteDeliveryProgressResult>;
public record DeleteDeliveryProgressResult(bool IsSuccess);

public class DeleteDeliveryCommandValidator : AbstractValidator<DeleteDeliveryProgressCommand>
{
    public DeleteDeliveryCommandValidator()
    {
        RuleFor(x => x.deliveryNo).NotEmpty().WithMessage("Delivery Progress DeliveryNo is required");
    }
}

public class DeleteDeliveryProgressHandler(DeliveryDbContext dbContext)
    : ICommandHandler<DeleteDeliveryProgressCommand, DeleteDeliveryProgressResult>
{
    public async Task<DeleteDeliveryProgressResult> Handle(DeleteDeliveryProgressCommand command, CancellationToken cancellationToken)
    {
        
        var dataDeliveryProgresses = await dbContext.DeliveryProgresses
            .Where(x => x.DeliveryNo == command.deliveryNo)
            .ToListAsync(cancellationToken);

        if (!dataDeliveryProgresses.Any())
        {
            throw new DeliveryProgressNotFoundException(command.deliveryNo);
        }
        
        dbContext.RemoveRange(dataDeliveryProgresses);
        await dbContext.SaveChangesAsync(cancellationToken);
        
        return new DeleteDeliveryProgressResult(true);
        
    }
}