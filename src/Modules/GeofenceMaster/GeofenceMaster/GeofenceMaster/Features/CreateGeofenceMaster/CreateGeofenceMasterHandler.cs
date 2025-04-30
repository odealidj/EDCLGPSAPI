using GeofenceMaster.Data.Repository.IRepository;
using GeofenceMaster.GeofenceMaster.Dtos;
using GeofenceMaster.GeofenceMaster.Models;
using Shared.Contracts.CQRS;

namespace GeofenceMaster.GeofenceMaster.Features.CreateGeofenceMaster;

public record CreateGeofenceMasterCommand(GeofenceMasterDto GeofenceMaster)
    : ICommand<CreateGeofenceMasterResult>;
public record CreateGeofenceMasterResult(Guid Id);

public class CreateGeofenceMasterCommandValidator : AbstractValidator<CreateGeofenceMasterCommand>
{
    public CreateGeofenceMasterCommandValidator()
    {
        RuleFor(x => x.GeofenceMaster.VendorName).NotEmpty().WithMessage("Vendor Name is required");
        ////RuleFor(x => x.GeofenceMaster.LpcdId).NotEmpty().WithMessage("LPCD is required");
    }
}

internal class CreateGeofenceMasterHandler(IGeofenceMasterRepository repository)
    : ICommandHandler<CreateGeofenceMasterCommand, CreateGeofenceMasterResult>
{
    public async Task<CreateGeofenceMasterResult> Handle(CreateGeofenceMasterCommand command, CancellationToken cancellationToken)
    {
        //create Basket entity from command object
        //save to database
        //return result

        var geofenceMaster = CreateNewGeofenceMaster(command.GeofenceMaster);        

        await repository.CreateGeofenceMaster(geofenceMaster, cancellationToken);

        return new CreateGeofenceMasterResult(geofenceMaster.Id);
    }
    
    private GpsVendor CreateNewGeofenceMaster(GeofenceMasterDto geofenceMasterDto)
    {
        
        //string vendorName, string lpcdId , string timezone, bool requiredAuth
        
        // create new GpsVendor
        var newGpsVendor = GpsVendor.Create(
            Guid.NewGuid(),
            geofenceMasterDto.VendorName,
            ////geofenceMasterDto.LpcdId,
            geofenceMasterDto.Timezone,
            geofenceMasterDto.RequiredAuth,
            geofenceMasterDto.AuthType,
            geofenceMasterDto.Username,
            geofenceMasterDto.Password,
            geofenceMasterDto.ProcessingStrategy,
            geofenceMasterDto.ProcessingStrategyPathData,
            geofenceMasterDto.ProcessingStrategyPathKey
            );
            

        //Guid gpsVendorId, string baseUrl, string method, string authtype, 
        // JsonObject? headers, JsonObject? @params, JsonObject bodies
        geofenceMasterDto.GeofenceMasterEndpoints.ForEach(item =>
        {
            newGpsVendor.AddGpsVendorEndpoint(
                Guid.NewGuid(),
                newGpsVendor.Id,
                item.BaseUrl,
                item.Method,
                item.ContentType,
                item.Headers,
                item.Params,
                item.Bodies,
                item.VarParams
            );
        });
        
        
        geofenceMasterDto.GeofenceMasterAuths?.ForEach(item =>
        {
            newGpsVendor.AddGpsVendorAuth(
                Guid.NewGuid(),
                newGpsVendor.Id,
                item.BaseUrl,
                item.Method,
                item.Authtype,
                item.ContentType,
                item.Username,
                item.Password,
                item.TokenPath,
                item.Headers,
                item.Params,
                item.Bodies
                );
        });

        geofenceMasterDto.GeofenceMasterMappings.ForEach(item =>
        {
            newGpsVendor.AddMapping(
                null,
                newGpsVendor.Id,
                item.ResponseField,
                item.MappedField
            );
              
        });

        foreach (var lpcd in geofenceMasterDto.Lpcds)
        {
            newGpsVendor.AddLpcd(
                Guid.NewGuid(),
                newGpsVendor.Id,
                lpcd.Lpcd
                );
        }

        return newGpsVendor;
    }
}