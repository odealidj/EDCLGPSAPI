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
public class GetVendorsHandler(
    IVendorMasterRepository repository,
    IMapper mapper)
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

        // mapping entity ke dto via AutoMapper
        var dtos = mapper.Map<List<VendorDto>>(vendors);

        var paginatedResult = new PaginatedResult<VendorDto>(
            normalizedPageIndex,
            p.PageSize,
            totalCount,
            dtos);

        return new GetVendorsResult(paginatedResult);
    }
}
