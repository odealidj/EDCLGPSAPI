using GeofenceMaster.GeofenceMaster.Dtos;
using GeofenceMaster.GeofenceMaster.Exceptions;
using Shared.Contracts.CQRS;

namespace GeofenceMaster.GeofenceMaster.Features.UpdateGeofenceMaster;

public record UpdateGeofenceMasterCommand(GeofenceMasterDto GeofenceMaster)
    : ICommand<UpdateGeofenceMasterResult>;
public record UpdateGeofenceMasterResult(bool IsSuccess);

public class UpdateGeofenceMasterCommandValidator : AbstractValidator<UpdateGeofenceMasterCommand>
{
    public UpdateGeofenceMasterCommandValidator()
    {
        RuleFor(x => x.GeofenceMaster.Id).NotEmpty().WithMessage("Id Vendor is required");
        RuleFor(x => x.GeofenceMaster.VendorName).NotEmpty().WithMessage("Vendor Name is required");
        RuleFor(x => x.GeofenceMaster.LpcdId).NotEmpty().WithMessage("LPCD is required");
    }
}

public class UpdateGeofenceMasterHandler(GeofenceMasterDbContext dbContext)
    : ICommandHandler<UpdateGeofenceMasterCommand, UpdateGeofenceMasterResult>
{
    public async Task<UpdateGeofenceMasterResult> Handle(UpdateGeofenceMasterCommand command, CancellationToken cancellationToken)
    {
        var geofenceMasters = await dbContext.GpsVendors
            .Where(x => x.Id == command.GeofenceMaster.Id)
            .Include( x => x.GpsVendorAuths)
            .ToListAsync(cancellationToken);

        if (!geofenceMasters.Any())
        {
            if (command.GeofenceMaster.Id != null)
                throw new GeofenceMasterNotFoundException(command.GeofenceMaster.Id.Value);
        }
        
        // Step 2: Update properti utama GpsVendor
        geofenceMasters.First().VendorName = command.GeofenceMaster.VendorName;
        geofenceMasters.First().LpcdId = command.GeofenceMaster.LpcdId;
        geofenceMasters.First().Timezone = command.GeofenceMaster.Timezone;
        geofenceMasters.First().RequiredAuth = command.GeofenceMaster.RequiredAuth;
        geofenceMasters.First().ProcessingStrategy = command.GeofenceMaster.ProcessingStrategy;
        
        
        // Step 3: Update atau tambahkan GpsVendorEndpoint berdasarkan Items di GeofenceMasterDto
        foreach (var itemDto in command.GeofenceMaster.GeofenceMasterEndpoints)
        {
            //var existingItem = geofenceMasters.First().Items
            //    .FirstOrDefault(x => x.Id == itemDto.Id);
            
            geofenceMasters.First().AddGpsVendorEndpoint(
                itemDto.Id,
                geofenceMasters.First().Id,
                itemDto.BaseUrl,
                itemDto.Method,
                itemDto.Headers,
                itemDto.Params,
                itemDto.Bodies);
        }
        
        
        // Step 3: Update atau tambahkan GpsVendorAuth berdasarkan Items di GeofenceMasterDto
        foreach (var itemDto in command.GeofenceMaster.GeofenceMasterAuths)
        {
            //var existingItem = geofenceMasters.First().Items
            //    .FirstOrDefault(x => x.Id == itemDto.Id);
            
            geofenceMasters.First().AddGpsVendorAuth(
                itemDto.Id,
                geofenceMasters.First().Id,
                itemDto.BaseUrl,
                itemDto.Method,
                itemDto.Authtype,
                itemDto.TokenPath,
                itemDto.Headers,
                itemDto.Params,
                itemDto.Bodies);

           
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new UpdateGeofenceMasterResult(true);
        

    }
    
    
}