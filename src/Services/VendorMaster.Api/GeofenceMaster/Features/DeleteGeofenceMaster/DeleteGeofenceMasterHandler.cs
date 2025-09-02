using GeofenceMaster.GeofenceMaster.Exceptions;
using GeofenceMaster.GeofenceMaster.Models;
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
            .Include(v => v.GpsVendorEndpoints) // Include child collection
            .Include(v => v.GpsVendorAuth) // Include child collection
            .Include(v => v.Mappings) //
            .Include( v => v.Lpcds) // Include child collection
            .ToListAsync(cancellationToken);

        if (!geofenceMasters.Any())
        {
            throw new GeofenceMasterNotFoundException(command.Id);
        }

        dbContext.GpsVendorEndpoints.RemoveRange(geofenceMasters.First().GpsVendorEndpoints);
        
        var gpsVendorAuth = geofenceMasters.First().GpsVendorAuth;
        if (gpsVendorAuth != null)
            dbContext.GpsVendorAuths.RemoveRange(gpsVendorAuth);
        
        dbContext.Mappings.RemoveRange(geofenceMasters.First().Mappings);
        dbContext.Lpcds.RemoveRange(geofenceMasters.First().Lpcds);
        
        // Hapus GpsVendor
        dbContext.GpsVendors.Remove(geofenceMasters.First());
        
        // Simpan perubahan
        await dbContext.SaveChangesAsync(cancellationToken);
        
        return new DeleteGeofenceMasterResult(true);
    }
}