using DeliveryTracking.Application.DTOs;
using DeliveryTracking.Application.Ports;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Shared.Contracts.CQRS;

namespace DeliveryTracking.Application.UseCases.GetActiveDeliverySessions;

public record GetActiveDeliverySessionsQuery(int Limit = 100)
    : IQuery<IReadOnlyList<DeliveryTrackingSessionDto>>;

public class GetActiveDeliverySessionsQueryValidator : AbstractValidator<GetActiveDeliverySessionsQuery>
{
    public GetActiveDeliverySessionsQueryValidator()
    {
        RuleFor(x => x.Limit)
            .GreaterThan(0).WithMessage("Limit harus lebih besar dari 0.")
            .LessThanOrEqualTo(1000).WithMessage("Limit maksimal 1000 data.");
    }
}

public class GetActiveDeliverySessionsQueryHandler(
    IDeliveryTrackingRepository repository,
    ILogger<GetActiveDeliverySessionsQueryHandler> logger)
    : IQueryHandler<GetActiveDeliverySessionsQuery, IReadOnlyList<DeliveryTrackingSessionDto>>
{
    public async Task<IReadOnlyList<DeliveryTrackingSessionDto>> Handle(
        GetActiveDeliverySessionsQuery request,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Mengambil daftar sesi delivery aktif dengan limit {Limit}", request.Limit);
        var sessions = await repository.GetActiveDeliverySessionsAsync(request.Limit, cancellationToken);
        var dtos = new List<DeliveryTrackingSessionDto>(sessions.Count);

        foreach (var s in sessions)
        {
            dtos.Add(new DeliveryTrackingSessionDto(
                Id: s.Id,
                DeliveryNo: s.DeliveryNo,
                Lpcd: s.Lpcd,
                VendorId: s.VendorId,
                VendorName: null,
                Key: s.MatchKey,
                Val: s.MatchValue,
                CleanValue: s.CleanMatchValue,
                DriverKtp: s.DriverKtp,
                Status: s.Status,
                StartedAt: s.StartedAt,
                ArrivedAt: s.ArrivedAt,
                CompletedAt: s.CompletedAt
            ));
        }

        return dtos;
    }
}
