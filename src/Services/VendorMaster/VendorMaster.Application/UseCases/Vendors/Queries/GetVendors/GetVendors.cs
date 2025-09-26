namespace VendorMaster.Application.UseCases.Vendors.Queries.GetVendors;

// request & result query get vendors
public record GetVendorsQuery(GetVendorsDto Params) : IQuery<GetVendorsResult>;
public record GetVendorsResult(PaginatedResult<VendorDto> Vendors);

// validasi parameter pagination get vendors
public class GetVendorsValidator : AbstractValidator<GetVendorsQuery>
{
    public GetVendorsValidator()
    {
        RuleFor(x => x.Params.PageIndex).GreaterThanOrEqualTo(0)
            .WithMessage("PageIndex tidak boleh negatif.");
        RuleFor(x => x.Params.PageSize).GreaterThan(0)
            .WithMessage("PageSize harus lebih dari 0.");
    }
}

// handler use case get list vendor gps
public class GetVendorsHandler(IVendorMasterRepository repository)
    : IQueryHandler<GetVendorsQuery, GetVendorsResult>
{
    public async Task<GetVendorsResult> Handle(GetVendorsQuery query, CancellationToken cancellationToken)
    {
        var p = query.Params;
        var normalizedPageIndex = p.PageIndex < 1 ? 1 : p.PageIndex;

        // load vendor dari db terpaginasi
        var vendors = await repository.GetVendorsAsync(
            p.Id,
            p.VendorName,
            p.Lpcd,
            normalizedPageIndex,
            p.PageSize,
            asNoTracking: true,
            cancellationToken: cancellationToken);

        // hitung total baris
        var totalCount = await repository.GetVendorsCountAsync(
            p.Id,
            p.VendorName,
            p.Lpcd,
            cancellationToken);

        // mapping entity ke dto
        var dtos = vendors.Select(v => new VendorDto
        {
            Id = v.Id,
            VendorCode = v.VendorCode,
            VendorName = v.VendorName,
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
        }).ToList();

        var paginatedResult = new PaginatedResult<VendorDto>(
            normalizedPageIndex,
            p.PageSize,
            totalCount,
            dtos);

        return new GetVendorsResult(paginatedResult);
    }
}
