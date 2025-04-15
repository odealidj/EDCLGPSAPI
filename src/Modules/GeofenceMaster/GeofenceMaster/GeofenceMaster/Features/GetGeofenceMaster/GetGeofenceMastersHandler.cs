using GeofenceMaster.Data.Repository.IRepository;
using GeofenceMaster.GeofenceMaster.Dtos;
using Shared.Contracts.CQRS;
using Shared.Pagination;

namespace GeofenceMaster.GeofenceMaster.Features.GetGeofenceMaster;

public record GetGeofenceMasterQuery(GetGeoferenceMasterDto GetGeoferenceMaster)
    : IQuery<GetGeofenceMastersResult>;
public record GetGeofenceMastersResult(PaginatedResult<GeofenceMasterDto> GeofenceMasters);

public class GetGeofenceMastersQueryValidator : AbstractValidator<GetGeofenceMasterQuery>
{
    public GetGeofenceMastersQueryValidator()
    {
        RuleFor(x => x.GetGeoferenceMaster.PageIndex).NotEmpty().WithMessage("PageIndex is required");
        RuleFor(x => x.GetGeoferenceMaster.PageSize).NotEmpty().WithMessage("PageSize is required");
        
        RuleFor(x => x.GetGeoferenceMaster.PageIndex).GreaterThan(0).WithMessage("PageIndex must be greater than 0");
    }
}

public class GetGeofenceMastersHandler(IGeofenceMasterRepository repository)
    : IQueryHandler<GetGeofenceMasterQuery, GetGeofenceMastersResult>
{
    public async Task<GetGeofenceMastersResult> Handle(GetGeofenceMasterQuery query, CancellationToken cancellationToken)
    {
        

        // Panggil kedua metode secara berurutan untuk menghindari masalah DbContext
        var vendorsTask =  await repository.GetGeofenceMaster(
            query.GetGeoferenceMaster.VendorName, 
            query.GetGeoferenceMaster.PageIndex, 
            query.GetGeoferenceMaster.PageSize);

        var totalCountTask = await repository.GetGeofenceMasterCount(
            query.GetGeoferenceMaster.VendorName);
        


        // Tunggu kedua task selesai
        //var vendors = await vendorsTask;  // Data untuk vendors
        //var totalCount = await totalCountTask;  // Jumlah total data
        
        
        var getVendors = vendorsTask.Select(gpsVendor => new GeofenceMasterDto
        {
            Id = gpsVendor.Id,
            VendorName = gpsVendor.VendorName,
            LpcdId = gpsVendor.LpcdId,
            Timezone = gpsVendor.Timezone,
            RequiredAuth = gpsVendor.RequiredAuth != null && gpsVendor.RequiredAuth.Value,
            GeofenceMasterEndpoints = gpsVendor.GpsVendorEndpoints.Select(item => new GeofenceMasterEndpointDto
            {
                Id = item.Id,
                GpsVendorId = item.GpsVendorId,
                BaseUrl = item.BaseUrl,
                Method = item.Method,
                Headers = item.Headers,
                Params = item.Params,
                Bodies = item.Bodies
            }).ToList(),
            GeofenceMasterAuths = gpsVendor.GpsVendorAuths.Select(item => new GeofenceMasterAuthDto
            {
                Id = item.Id,
                GpsVendorId = item.GpsVendorId,
                BaseUrl = item.BaseUrl,
                Method = item.Method,
                Authtype = item.Authtype,
                Headers = item.Headers,
                Params = item.Params,
                Bodies = item.Bodies
            }).ToList()
        }).ToList();
        
        return new GetGeofenceMastersResult(
            new PaginatedResult<GeofenceMasterDto>(
                query.GetGeoferenceMaster.PageIndex,
                query.GetGeoferenceMaster.PageSize,
                totalCountTask,
                getVendors)
        );
        

    }
}