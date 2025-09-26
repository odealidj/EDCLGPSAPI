namespace VendorMaster.Application.UseCases.Vendors.Queries.GetVendorById;

// request & result get vendor by id
public record GetVendorByIdQuery(Guid Id) : IQuery<GetVendorByIdResult>;
public record GetVendorByIdResult(VendorDto Vendor);

// handler use case get detail vendor
public class GetVendorByIdHandler(IVendorMasterRepository repository)
    : IQueryHandler<GetVendorByIdQuery, GetVendorByIdResult>
{
    public async Task<GetVendorByIdResult> Handle(GetVendorByIdQuery query, CancellationToken cancellationToken)
    {
        var v = await repository.GetVendorByIdAsync(query.Id, asNoTracking: true, cancellationToken);
        if (v == null)
        {
            throw new VendorNotFoundException(query.Id);
        }

        var dto = new VendorDto
        {
            Id = v.Id,
            VendorCode = v.VendorCode,
            VendorName = v.VendorName,
            Lpcd = v.Lpcds.FirstOrDefault()?.Lpcd ?? string.Empty,
            ProtocolType = v.ProtocolType,
            Timezone = v.Timezone,
            IsActive = v.IsActive,
            Version = v.Version,
            ProcessingStrategy = v.ProcessingStrategy,
            ProcessingStrategyPathData = v.ProcessingStrategyPathData,
            ProcessingStrategyPathKey = v.ProcessingStrategyPathKey,
            RequiredAuth = v.GpsVendorAuth != null,
            AuthType = v.GpsVendorAuth?.AuthType ?? "NoAuth",
            Username = v.GpsVendorAuth?.Username,
            Password = v.GpsVendorAuth?.Password,
            Endpoints = v.GpsVendorEndpoints.Select(ep => new VendorEndpointDto
            {
                Id = ep.Id,
                GpsVendorId = ep.VendorId,
                EndpointType = ep.EndpointType,
                BaseUrl = ep.BaseUrl,
                Method = ep.Method,
                ContentType = ep.ContentType,
                Headers = ep.Headers,
                Params = ep.Params,
                Bodies = ep.Bodies,
                VarParams = ep.VarParams,
                MaxPath = ep.MaxPath
            }).ToList(),
            Auth = v.GpsVendorAuth == null ? null : new VendorAuthDto
            {
                Id = v.GpsVendorAuth.Id,
                GpsVendorId = v.GpsVendorAuth.VendorId,
                BaseUrl = v.GpsVendorAuth.BaseUrl,
                Method = v.GpsVendorAuth.Method,
                Authtype = v.GpsVendorAuth.AuthType,
                ContentType = v.GpsVendorAuth.ContentType,
                Username = v.GpsVendorAuth.Username,
                Password = v.GpsVendorAuth.Password,
                TokenPath = v.GpsVendorAuth.TokenPath,
                TokenExpiresInPath = v.GpsVendorAuth.TokenExpiresInPath,
                TokenTtlSeconds = v.GpsVendorAuth.TokenTtlSeconds,
                Headers = v.GpsVendorAuth.Headers,
                Params = v.GpsVendorAuth.Params,
                Bodies = v.GpsVendorAuth.Bodies
            },
            Mappings = v.Mappings.Select(m => new VendorMappingDto
            {
                Id = m.Id,
                GpsVendorId = m.VendorId,
                ResponseField = m.ResponseField,
                MappedField = m.MappedField,
                DataType = m.DataType
            }).ToList(),
            Lpcds = v.Lpcds.Select(l => new VendorLpcdDto
            {
                Id = l.Id,
                GpsVendorId = l.VendorId,
                Lpcd = l.Lpcd
            }).ToList()
        };

        return new GetVendorByIdResult(dto);
    }
}
