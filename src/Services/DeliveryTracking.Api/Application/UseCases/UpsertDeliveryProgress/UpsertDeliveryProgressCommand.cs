using DeliveryTracking.Application.DTOs;
using DeliveryTracking.Application.Ports;
using DeliveryTracking.Domain.Entities;
using Shared.Contracts.CQRS;

namespace DeliveryTracking.Application.UseCases.UpsertDeliveryProgress;

public record UpsertDeliveryProgressCommand(DeliveryProgressDto Progress)
    : ICommand<SaveDeliveryProgressResult>;

public class UpsertDeliveryProgressCommandValidator : AbstractValidator<UpsertDeliveryProgressCommand>
{
    public UpsertDeliveryProgressCommandValidator()
    {
        RuleFor(x => x.Progress.DeliveryNo)
            .NotEmpty().WithMessage("Nomor DO / delivery wajib diisi.");

        RuleFor(x => x.Progress.PlatNo)
            .NotEmpty().WithMessage("Nomor plat armada wajib diisi.");

        RuleFor(x => x.Progress.NoKtp)
            .NotEmpty().WithMessage("Nomor KTP supir wajib diisi.");
    }
}

public class UpsertDeliveryProgressCommandHandler(
    IDeliveryTrackingRepository repository,
    ILogger<UpsertDeliveryProgressCommandHandler> logger)
    : ICommandHandler<UpsertDeliveryProgressCommand, SaveDeliveryProgressResult>
{
    public async Task<SaveDeliveryProgressResult> Handle(
        UpsertDeliveryProgressCommand request,
        CancellationToken cancellationToken)
    {
        var dto = request.Progress;
        var vendorName = dto.VendorName;
        var lpcd = dto.Lpcd;

        // resolusi otomatis vendor dan lpcd jika belum terisi di payload
        if (!string.IsNullOrWhiteSpace(dto.PlatNo) && string.IsNullOrWhiteSpace(vendorName))
        {
            if (!string.IsNullOrWhiteSpace(lpcd))
            {
                var vendorLookup = await repository.GetVendorByLpcdAsync(lpcd, cancellationToken);
                if (vendorLookup != null)
                {
                    vendorName = vendorLookup.GpsVendorName;
                }
            }
            else
            {
                var vendorByPlat = await repository.GetVendorByPlatNoAsync(dto.PlatNo, cancellationToken);
                if (vendorByPlat != null)
                {
                    vendorName = vendorByPlat.VendorName;
                    var lpcdLookup = await repository.GetLpcdByVendorNameAsync(vendorName, cancellationToken);
                    if (lpcdLookup != null)
                    {
                        lpcd = lpcdLookup.Lpcd;
                    }
                }
            }
        }

        var entity = DeliveryProgress.Create(
            id: dto.Id ?? Guid.NewGuid(),
            deliveryNo: dto.DeliveryNo,
            platNo: dto.PlatNo,
            noKtp: dto.NoKtp,
            vendorName: vendorName,
            lpcd: lpcd);

        var result = await repository.UpsertDeliveryProgressAsync(entity, cancellationToken);
        logger.LogInformation("Berhasil upsert delivery progress '{DeliveryNo}' (Id: {Id}, Created: {IsCreated})",
            result.DeliveryNo, result.Id, result.IsCreated);

        return result;
    }
}
