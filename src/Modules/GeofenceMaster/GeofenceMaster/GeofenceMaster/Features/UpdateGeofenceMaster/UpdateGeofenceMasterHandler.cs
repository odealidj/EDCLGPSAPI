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
        ////RuleFor(x => x.GeofenceMaster.LpcdId).NotEmpty().WithMessage("LPCD is required");
    }
}

public class UpdateGeofenceMasterHandler(GeofenceMasterDbContext dbContext)
    : ICommandHandler<UpdateGeofenceMasterCommand, UpdateGeofenceMasterResult>
{
    public async Task<UpdateGeofenceMasterResult> Handle(UpdateGeofenceMasterCommand command, CancellationToken cancellationToken)
    {
        var geofenceMasters = await dbContext.GpsVendors
            .Where(x => x.Id == command.GeofenceMaster.Id)
            .Include(x=> x.GpsVendorEndpoints)
            .Include( x => x.GpsVendorAuths)
            .Include(x => x.Mappings)
            .Include( x => x.Lpcds)
            .ToListAsync(cancellationToken);

        if (geofenceMasters.Count == 0)
        {
            if (command.GeofenceMaster.Id != null)
                throw new GeofenceMasterNotFoundException(command.GeofenceMaster.Id.Value);
        }
        
        //Step 1: remove GpsVendorLpcds yang ada
        ////dbContext.GpsVendorLpcds.RemoveRange(geofenceMasters.First().GpsVendorLpcds.Distinct());
        
        // Step 2: Update GpsVendor
        geofenceMasters.First().VendorName = command.GeofenceMaster.VendorName;
        geofenceMasters.First().Timezone = command.GeofenceMaster.Timezone;
        geofenceMasters.First().RequiredAuth = command.GeofenceMaster.RequiredAuth;
        geofenceMasters.First().ProcessingStrategy = command.GeofenceMaster.ProcessingStrategy;
        geofenceMasters.First().ProcessingStrategyPathData = command.GeofenceMaster.ProcessingStrategyPathData;
        geofenceMasters.First().ProcessingStrategyPathKey = command.GeofenceMaster.ProcessingStrategyPathKey;
        
        // Step 3: Update atau tambahkan GpsVendorEndpoint berdasarkan Items di GeofenceMasterDto
        foreach (var itemDto in command.GeofenceMaster.GeofenceMasterEndpoints)
        {

            geofenceMasters.First().AddGpsVendorEndpoint(
                itemDto.Id,
                geofenceMasters.First().Id,
                itemDto.BaseUrl,
                itemDto.Method,
                itemDto.ContentType,
                itemDto.Headers,
                itemDto.Params,
                itemDto.Bodies);
        }
        
        

        if (command.GeofenceMaster.GeofenceMasterAuths != null)
            foreach (var itemDto in command.GeofenceMaster.GeofenceMasterAuths)
            {
                geofenceMasters.First().AddGpsVendorAuth(
                    itemDto.Id,
                    geofenceMasters.First().Id,
                    itemDto.BaseUrl,
                    itemDto.Method,
                    itemDto.Authtype,
                    itemDto.ContentType,
                    itemDto.Username,
                    itemDto.Password,
                    itemDto.TokenPath,
                    itemDto.Headers,
                    itemDto.Params,
                    itemDto.Bodies);
            }
        
        
        foreach (var itemDto in command.GeofenceMaster.GeofenceMasterMappings)
        {
            geofenceMasters.First().AddMapping(
                itemDto.Id,
                geofenceMasters.First().Id,
                itemDto.ResponseField,
                itemDto.MappedField);
        }
        
        foreach (var lpcd in command.GeofenceMaster.Lpcds)
        {
            geofenceMasters.First().AddLpcd(
                lpcd.Id,        
                geofenceMasters.First().Id,
                lpcd.Lpcd
                );
            
        }
        

        await dbContext.SaveChangesAsync(cancellationToken);

        return new UpdateGeofenceMasterResult(true);
        

    }
    
    
}