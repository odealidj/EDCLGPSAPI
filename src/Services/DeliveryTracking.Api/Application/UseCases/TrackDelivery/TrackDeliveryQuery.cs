using DeliveryTracking.Application.DTOs;
using DeliveryTracking.Application.Ports;
using Shared.Contracts.CQRS;

namespace DeliveryTracking.Application.UseCases.TrackDelivery;

public record TrackDeliveryQuery(string DeliveryNo, int Density = 1)
    : IQuery<IReadOnlyList<TrackDeliveryPointDto>>;

public class TrackDeliveryQueryValidator : AbstractValidator<TrackDeliveryQuery>
{
    public TrackDeliveryQueryValidator()
    {
        RuleFor(x => x.DeliveryNo)
            .NotEmpty().WithMessage("Nomor DO / delivery tidak boleh kosong.");

        RuleFor(x => x.Density)
            .GreaterThan(0).WithMessage("Density harus lebih besar dari 0.");
    }
}

public class TrackDeliveryQueryHandler(
    IDeliveryTrackingRepository repository,
    ILogger<TrackDeliveryQueryHandler> logger) : IQueryHandler<TrackDeliveryQuery, IReadOnlyList<TrackDeliveryPointDto>>
{
    public async Task<IReadOnlyList<TrackDeliveryPointDto>> Handle(
        TrackDeliveryQuery request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Mengambil jejak GPS delivery '{DeliveryNo}' dengan density {Density}",
            request.DeliveryNo, request.Density);

        return await repository.GetTrackDeliveryAsync(request.DeliveryNo, request.Density, cancellationToken);
    }
}
