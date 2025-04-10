using GeofenceMaster.GeofenceMaster.Exceptions;
using Shared.Contracts.CQRS;

namespace GeofenceMaster.GeofenceMaster.Features.DeleteGeofenceMaster;

public record DeleteGeofenceMasterCommand(Guid Id)
    : ICommand<DeleteGeofenceMasterResult>;
public record DeleteGeofenceMasterResult(bool IsSuccess);


public class DeleteGeofenceMasterCommandValidator : AbstractValidator<DeleteGeofenceMasterCommand>
{
    public DeleteGeofenceMasterCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("GpsVendor Id is required");
    }
}

public class DeleteGeofenceMasterHandler(GeofenceMasterDbContext dbContext)
    : ICommandHandler<DeleteGeofenceMasterCommand, DeleteGeofenceMasterResult>
{
    public async Task<DeleteGeofenceMasterResult> Handle(DeleteGeofenceMasterCommand command, CancellationToken cancellationToken)
    {
        
        var geofenceMasters = await dbContext.GpsVendors
            .Where(x => x.Id == command.Id)
            .Include(v => v.Items) // Include child collection
            .ToListAsync(cancellationToken);

        if (!geofenceMasters.Any())
        {
            throw new GeofenceMasterNotFoundException(command.Id);
        }

        // Hapus semua GpsVendorAuth terkait
        dbContext.GpsVendorAuths.RemoveRange(geofenceMasters.First().Items);
        
        // Hapus GpsVendor
        dbContext.GpsVendors.Remove(geofenceMasters.First());
        
        // Simpan perubahan
        await dbContext.SaveChangesAsync(cancellationToken);
        
        return new DeleteGeofenceMasterResult(true);
    }
}