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
            query.GetGeoferenceMaster.Id,
            query.GetGeoferenceMaster.VendorName, 
            query.GetGeoferenceMaster.PageIndex, 
            query.GetGeoferenceMaster.PageSize, cancellationToken: cancellationToken);

        var totalCountTask = await repository.GetGeofenceMasterCount(
            query.GetGeoferenceMaster.Id,
            query.GetGeoferenceMaster.VendorName, cancellationToken);
        

        // Tunggu kedua task selesai
        //var vendors = await vendorsTask;  // Data untuk vendors
        //var totalCount = await totalCountTask;  // Jumlah total data
        
        var getVendors = vendorsTask.Select(gpsVendor => new GeofenceMasterDto
        {
            Id = gpsVendor.Id,
            VendorName = gpsVendor.VendorName,
            Timezone = gpsVendor.Timezone,
            RequiredAuth = gpsVendor.RequiredAuth != null && gpsVendor.RequiredAuth.Value,
            AuthType = gpsVendor.AuthType,
            Username = gpsVendor.Username,
            Password = gpsVendor.Password,
            ProcessingStrategy = gpsVendor.ProcessingStrategy,
            ProcessingStrategyPathData = gpsVendor.ProcessingStrategyPathData,
            ProcessingStrategyPathKey = gpsVendor.ProcessingStrategyPathKey,
            GeofenceMasterEndpoints = gpsVendor.GpsVendorEndpoints.Select(item => new GeofenceMasterEndpointDto
            {
                Id = item.Id,
                GpsVendorId = item.GpsVendorId,
                BaseUrl = item.BaseUrl,
                Method = item.Method,
                ContentType = item.ContentType,
                Headers = item.Headers,
                Params = item.Params,
                Bodies = item.Bodies,
                VarParams = item.VarParams,
                MaxPath = item.MaxPath
            }).ToList(),
            GeofenceMasterAuth = gpsVendor.GpsVendorAuth != null?
                new GeofenceMasterAuthDto(
                    gpsVendor.GpsVendorAuth.Id,
                    gpsVendor.GpsVendorAuth.GpsVendorId,
                    gpsVendor.GpsVendorAuth.BaseUrl,
                    gpsVendor.GpsVendorAuth.Method,
                    gpsVendor.GpsVendorAuth.Authtype,
                    gpsVendor.GpsVendorAuth.ContentType,
                    gpsVendor.GpsVendorAuth.Username,
                    gpsVendor.GpsVendorAuth.Password,
                    gpsVendor.GpsVendorAuth.TokenPath,
                    gpsVendor.GpsVendorAuth.Headers,
                    gpsVendor.GpsVendorAuth.Params,
                    gpsVendor.GpsVendorAuth.Bodies
                ) : null,
            /*
            GeofenceMasterAuths = gpsVendor.GpsVendorAuths.Select(item => new GeofenceMasterAuthDto
            {
                Id = item.Id,
                GpsVendorId = item.GpsVendorId,
                BaseUrl = item.BaseUrl,
                Method = item.Method,
                Authtype = item.Authtype,
                ContentType = item.ContentType,
                Username = item.Username,
                Password = item.Password,
                TokenPath = item.TokenPath,
                Headers = item.Headers,
                Params = item.Params,
                Bodies = item.Bodies
            }).ToList(),
            */
            GeofenceMasterMappings = gpsVendor.Mappings.Select(item => new GeofenceMasterMappingDto
            {
                Id = item.Id,
                GpsVendorId = item.GpsVendorId,
                ResponseField = item.ResponseField,
                MappedField = item.MappedField
            }).ToList(),
            Lpcds = gpsVendor.Lpcds.Select(item => new GeofenceMasterLpcdDto()
            {
                Id = item.Id,
                GpsVendorId = item.GpsVendorId,
                Lpcd = item.Lpcd
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