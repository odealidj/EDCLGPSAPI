using DeliveryTracking.Application.DTOs;
using DeliveryTracking.Application.Ports;
using DeliveryTracking.Domain.Exceptions;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Shared.Contracts.CQRS;

namespace DeliveryTracking.Application.UseCases.GetDeliverySession;

public record GetDeliverySessionQuery(string DeliveryNo)
    : IQuery<DeliveryTrackingSessionDto>;

public class GetDeliverySessionQueryValidator : AbstractValidator<GetDeliverySessionQuery>
{
    public GetDeliverySessionQueryValidator()
    {
        RuleFor(x => x.DeliveryNo)
            .NotEmpty().WithMessage("Nomor Delivery / DO tidak boleh kosong.");
    }
}

public class GetDeliverySessionQueryHandler(
    IDeliveryTrackingRepository repository,
    IMapper mapper,
    ILogger<GetDeliverySessionQueryHandler> logger)
    : IQueryHandler<GetDeliverySessionQuery, DeliveryTrackingSessionDto>
{
    public async Task<DeliveryTrackingSessionDto> Handle(
        GetDeliverySessionQuery request,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Mengambil data sesi delivery '{DeliveryNo}'", request.DeliveryNo);
        var session = await repository.GetDeliverySessionByDeliveryNoAsync(request.DeliveryNo, cancellationToken);
        if (session == null)
        {
            throw new DeliveryProgressNotFoundException(request.DeliveryNo);
        }

        string? vendorName = null;
        if (!string.IsNullOrWhiteSpace(session.Lpcd))
        {
            var vendorLookup = await repository.GetVendorByLpcdAsync(session.Lpcd, cancellationToken);
            vendorName = vendorLookup?.GpsVendorName;
        }

        var dto = mapper.Map<DeliveryTrackingSessionDto>(session);
        return string.IsNullOrWhiteSpace(vendorName) ? dto : dto with { VendorName = vendorName };
    }
}
