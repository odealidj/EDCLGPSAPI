using GPSInterfacing.Data;

namespace GPSInterfacing.Vendors.Features.CreateGpsVendor;

public record CreateGpsVendorCommand(GpsVendorDto GpsVendor)
    : ICommand<CreateGpsVendorResult>;
public record CreateGpsVendorResult(Guid Id);

public class CreateGpsVendorCommandValidator : AbstractValidator<CreateGpsVendorCommand>
{
    public CreateGpsVendorCommandValidator()
    {
        RuleFor(x => x.GpsVendor.VendorName).NotEmpty().WithMessage("Vendor Name is required");
        RuleFor(x => x.GpsVendor.LpcdId).NotEmpty().WithMessage("Lpcd Id is required");
        //RuleFor(x => x.Product.Price).GreaterThan(0).WithMessage("Price must be greater than 0");
    }
}


internal class CreateGpsVendorHandler
    (GPSInterfacingDbContext dbContext)
    : ICommandHandler<CreateGpsVendorCommand, CreateGpsVendorResult>
{
    public async Task<CreateGpsVendorResult> Handle(CreateGpsVendorCommand command, CancellationToken cancellationToken)
    {
        //create GpsVendor entity from command object
        //save to database
        //return result
        
        var gpsVendor = CreateNewProduct(command.GpsVendor);

        dbContext.GpsVendors.Add(gpsVendor);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateGpsVendorResult(gpsVendor.Id);

    }
    
    private GpsVendor CreateNewProduct(GpsVendorDto gpsVendorDto)
    {
        var gpsVendor = GpsVendor.Create(
            Guid.NewGuid(),
            gpsVendorDto.VendorName,
            gpsVendorDto.LpcdId,
            gpsVendorDto.Timezone,
            gpsVendorDto.RequiredAuth
            );

        return gpsVendor;
    }
}