using GeofenceMaster.Data.Repository.IRepository;
using GeofenceMaster.GeofenceMaster.Dtos;
using Shared.Contracts.CQRS;
using Shared.Pagination;

namespace GeofenceMaster.GeofenceMaster.Features.GetGeofenceMaster;

public record GetGeofenceMasterQuery(GetGeoferenceMasterDto GetGeoferenceMaster)
    : IQuery<GetGeofenceMastersResult>;
public record GetGeofenceMastersResult(PaginatedResult<GeofenceMasterDto> GeofenceMasters);


public class GetGeofenceMastersHandler(IGeofenceMasterRepository repository)
    : IQueryHandler<GetGeofenceMasterQuery, GetGeofenceMastersResult>
{
    public async Task<GetGeofenceMastersResult> Handle(GetGeofenceMasterQuery query, CancellationToken cancellationToken)
    {
        // get geofenceMasters
        var gpsVendors = await repository.GetGeofenceMaster(
            query.GetGeoferenceMaster.VendorName, 
            query.GetGeoferenceMaster.PageIndex, // GetGeoferenceMaster.PaginationRequest.PageIndex,
            query.GetGeoferenceMaster.PageSize,
            true, cancellationToken);
        
        //mapping product entity to ProductDto using Mapster
        var geofenceMasters = gpsVendors.Adapt<List<GeofenceMasterDto>>();
        
        return new GetGeofenceMastersResult(
            new PaginatedResult<GeofenceMasterDto>(
                query.GetGeoferenceMaster.PageIndex,
                query.GetGeoferenceMaster.PageSize,
                0,
                geofenceMasters)
        );

    }
}