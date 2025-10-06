namespace VendorMaster.Application.UseCases.Vendors.Queries.GetVendorById;

// request & result get vendor by id
public record GetVendorByIdQuery(Guid Id) : IQuery<GetVendorByIdResult>;
public record GetVendorByIdResult(VendorDto Vendor);

// handler use case get detail vendor
public class GetVendorByIdHandler(
    IVendorMasterRepository repository,
    IMapper mapper)
    : IQueryHandler<GetVendorByIdQuery, GetVendorByIdResult>
{
    public async Task<GetVendorByIdResult> Handle(GetVendorByIdQuery query, CancellationToken cancellationToken)
    {
        var v = await repository.GetVendorByIdAsync(query.Id, asNoTracking: true, cancellationToken);
        if (v == null)
        {
            throw new VendorNotFoundException(query.Id);
        }

        var dto = mapper.Map<VendorDto>(v);

        return new GetVendorByIdResult(dto);
    }
}
