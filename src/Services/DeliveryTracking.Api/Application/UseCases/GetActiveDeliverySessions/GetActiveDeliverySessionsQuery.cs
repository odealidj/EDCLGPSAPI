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
    IMapper mapper,
    ILogger<GetActiveDeliverySessionsQueryHandler> logger)
    : IQueryHandler<GetActiveDeliverySessionsQuery, IReadOnlyList<DeliveryTrackingSessionDto>>
{
    public async Task<IReadOnlyList<DeliveryTrackingSessionDto>> Handle(
        GetActiveDeliverySessionsQuery request,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Mengambil daftar sesi delivery aktif dengan limit {Limit}", request.Limit);
        var sessions = await repository.GetActiveDeliverySessionsAsync(request.Limit, cancellationToken);
        return mapper.Map<IReadOnlyList<DeliveryTrackingSessionDto>>(sessions);
    }
}
