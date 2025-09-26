using DeliveryTracking.Application.DTOs;
using DeliveryTracking.Application.Ports;
using DeliveryTracking.Domain.Exceptions;
using Shared.Contracts.CQRS;

namespace DeliveryTracking.Application.UseCases.GetDeliveryProgress;

public record GetDeliveryProgressQuery(string DeliveryNo) : IQuery<DeliveryProgressDto>;

public class GetDeliveryProgressQueryValidator : AbstractValidator<GetDeliveryProgressQuery>
{
    public GetDeliveryProgressQueryValidator()
    {
        RuleFor(x => x.DeliveryNo).NotEmpty().WithMessage("Nomor DO / delivery wajib diisi.");
    }
}

public class GetDeliveryProgressQueryHandler(
    IDeliveryTrackingRepository repository,
    ILogger<GetDeliveryProgressQueryHandler> logger) : IQueryHandler<GetDeliveryProgressQuery, DeliveryProgressDto>
{
    public async Task<DeliveryProgressDto> Handle(
        GetDeliveryProgressQuery request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Mengambil data progress DO '{DeliveryNo}'", request.DeliveryNo);
        var entity = await repository.GetDeliveryProgressByDeliveryNoAsync(request.DeliveryNo, cancellationToken);
        if (entity == null)
        {
            throw new DeliveryProgressNotFoundException(request.DeliveryNo);
        }

        return new DeliveryProgressDto
        {
            Id = entity.Id,
            DeliveryNo = entity.DeliveryNo,
            PlatNo = entity.PlatNo,
            NoKtp = entity.NoKtp,
            VendorName = entity.VendorName,
            Lpcd = entity.Lpcd
        };
    }
}
