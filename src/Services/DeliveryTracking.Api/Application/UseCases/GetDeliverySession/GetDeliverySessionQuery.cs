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

        return new DeliveryTrackingSessionDto(
            Id: session.Id,
            DeliveryNo: session.DeliveryNo,
            Lpcd: session.Lpcd,
            VendorId: session.VendorId,
            VendorName: vendorName,
            Key: session.MatchKey,
            Val: session.MatchValue,
            CleanValue: session.CleanMatchValue,
            DriverKtp: session.DriverKtp,
            Status: session.Status,
            StartedAt: session.StartedAt,
            ArrivedAt: session.ArrivedAt,
            CompletedAt: session.CompletedAt
        );
    }
}
