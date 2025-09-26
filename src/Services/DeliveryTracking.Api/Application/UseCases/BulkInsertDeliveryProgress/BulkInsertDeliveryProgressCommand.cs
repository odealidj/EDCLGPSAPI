using System.Diagnostics;
using DeliveryTracking.Application.DTOs;
using DeliveryTracking.Application.Ports;
using DeliveryTracking.Domain.Entities;
using Shared.Contracts.CQRS;

namespace DeliveryTracking.Application.UseCases.BulkInsertDeliveryProgress;

public record BulkInsertDeliveryProgressCommand(IReadOnlyList<DeliveryProgressDto> Items)
    : ICommand<BulkInsertResult>;

public class BulkInsertDeliveryProgressCommandValidator : AbstractValidator<BulkInsertDeliveryProgressCommand>
{
    public BulkInsertDeliveryProgressCommandValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Daftar item untuk bulk insert tidak boleh kosong.");
    }
}

public class BulkInsertDeliveryProgressCommandHandler(
    IDeliveryTrackingRepository repository,
    ILogger<BulkInsertDeliveryProgressCommandHandler> logger)
    : ICommandHandler<BulkInsertDeliveryProgressCommand, BulkInsertResult>
{
    public async Task<BulkInsertResult> Handle(
        BulkInsertDeliveryProgressCommand request,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var entities = request.Items.Select(dto => DeliveryProgress.Create(
            id: dto.Id ?? Guid.NewGuid(),
            deliveryNo: dto.DeliveryNo,
            platNo: dto.PlatNo,
            noKtp: dto.NoKtp,
            vendorName: dto.VendorName,
            lpcd: dto.Lpcd
        )).ToList();

        var rowsInserted = await repository.BulkInsertDeliveryProgressAsync(entities, cancellationToken);
        sw.Stop();

        logger.LogInformation("Selesai bulk insert {Count} delivery progress via PostgreSQL Binary COPY dalam {Elapsed}ms",
            rowsInserted, sw.ElapsedMilliseconds);

        return new BulkInsertResult(rowsInserted, nameof(DeliveryProgress), sw.Elapsed);
    }
}
