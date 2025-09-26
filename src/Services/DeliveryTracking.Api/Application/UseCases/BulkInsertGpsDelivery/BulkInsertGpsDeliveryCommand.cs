using System.Diagnostics;
using DeliveryTracking.Application.DTOs;
using DeliveryTracking.Application.Ports;
using DeliveryTracking.Domain.Entities;
using Shared.Contracts.CQRS;

namespace DeliveryTracking.Application.UseCases.BulkInsertGpsDelivery;

public record BulkInsertGpsDeliveryCommand(IReadOnlyList<BulkInsertGpsDeliveryItemDto> Items)
    : ICommand<BulkInsertResult>;

public class BulkInsertGpsDeliveryCommandValidator : AbstractValidator<BulkInsertGpsDeliveryCommand>
{
    public BulkInsertGpsDeliveryCommandValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Daftar titik GPS untuk bulk insert tidak boleh kosong.");
    }
}

public class BulkInsertGpsDeliveryCommandHandler(
    IDeliveryTrackingRepository repository,
    ILogger<BulkInsertGpsDeliveryCommandHandler> logger)
    : ICommandHandler<BulkInsertGpsDeliveryCommand, BulkInsertResult>
{
    public async Task<BulkInsertResult> Handle(
        BulkInsertGpsDeliveryCommand request,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var entities = request.Items.Select(dto => GpsDelivery.Create(
            id: Guid.NewGuid(),
            gpsVendorId: dto.GpsVendorId,
            gpsVendorName: dto.GpsVendorName,
            deliveryNo: dto.DeliveryNo,
            platNo: dto.PlatNo,
            lpcd: dto.Lpcd,
            datetime: dto.Datetime,
            x: dto.X,
            y: dto.Y,
            speed: dto.Speed,
            course: dto.Course,
            streetName: dto.StreetName,
            deviceId: dto.DeviceId,
            noKtp: dto.NoKtp
        )).ToList();

        var rowsInserted = await repository.BulkInsertGpsDeliveriesAsync(entities, cancellationToken);
        sw.Stop();

        logger.LogInformation("Selesai bulk insert {Count} titik GPS via PostgreSQL Binary COPY dalam {Elapsed}ms",
            rowsInserted, sw.ElapsedMilliseconds);

        return new BulkInsertResult(rowsInserted, nameof(GpsDelivery), sw.Elapsed);
    }
}
