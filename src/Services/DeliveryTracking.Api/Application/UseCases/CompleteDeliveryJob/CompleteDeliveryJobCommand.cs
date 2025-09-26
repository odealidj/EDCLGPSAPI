using DeliveryTracking.Application.DTOs;
using DeliveryTracking.Application.Ports;
using DeliveryTracking.Domain.Exceptions;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Shared.Contracts.CQRS;
using StackExchange.Redis;

namespace DeliveryTracking.Application.UseCases.CompleteDeliveryJob;

public record CompleteDeliveryJobCommand(CompleteDeliveryJobRequestDto Request)
    : ICommand<CompleteDeliveryJobResultDto>;

public class CompleteDeliveryJobCommandValidator : AbstractValidator<CompleteDeliveryJobCommand>
{
    public CompleteDeliveryJobCommandValidator()
    {
        RuleFor(x => x.Request.DeliveryNo)
            .NotEmpty().WithMessage("Nomor Delivery / DO (deliveryNo) tidak boleh kosong.");
    }
}

public class CompleteDeliveryJobCommandHandler(
    IDeliveryTrackingRepository repository,
    IConnectionMultiplexer redis,
    ILogger<CompleteDeliveryJobCommandHandler> logger)
    : ICommandHandler<CompleteDeliveryJobCommand, CompleteDeliveryJobResultDto>
{
    public async Task<CompleteDeliveryJobResultDto> Handle(
        CompleteDeliveryJobCommand command,
        CancellationToken cancellationToken)
    {
        var req = command.Request;
        var deliveryNo = req.DeliveryNo.Trim().ToUpperInvariant();

        // 1. Update status sesi di PostgreSQL menjadi 'COMPLETED'
        var completedSession = await repository.CompleteDeliverySessionAsync(
            deliveryNo: deliveryNo,
            completedBy: req.CompletedBy,
            source: req.Source,
            latitude: req.Latitude,
            longitude: req.Longitude,
            cancellationToken: cancellationToken
        );

        if (completedSession == null)
        {
            throw new DeliveryProgressNotFoundException(deliveryNo);
        }

        // 2. Bersihkan Hot Index dari Redis RAM (Un-indexing agar tidak dipoll lagi)
        try
        {
            var db = redis.GetDatabase();

            if (!string.IsNullOrWhiteSpace(completedSession.Lpcd) && !string.IsNullOrWhiteSpace(completedSession.CleanMatchValue))
            {
                var lpcdHashKey = $"delivery:active:lookup:{completedSession.Lpcd.ToUpperInvariant()}";
                await db.HashDeleteAsync(lpcdHashKey, completedSession.CleanMatchValue);
                await db.HashDeleteAsync("delivery:active:lookup", completedSession.CleanMatchValue);
                await db.KeyDeleteAsync($"delivery:active:{completedSession.CleanMatchValue}");
            }

            await db.SetRemoveAsync("delivery:active:set", deliveryNo);
            await db.KeyDeleteAsync($"delivery:session:{deliveryNo}");

            logger.LogInformation("Hot index Redis berhasil dibersihkan untuk delivery '{DeliveryNo}'", deliveryNo);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Gagal membersihkan cache Redis untuk delivery '{DeliveryNo}'.", deliveryNo);
        }

        logger.LogInformation("Sesi Delivery '{DeliveryNo}' berhasil diselesaikan (Status: COMPLETED)", deliveryNo);

        return new CompleteDeliveryJobResultDto(
            Status: 200,
            Message: "Delivery tracking job completed successfully",
            DeliveryNo: deliveryNo,
            CompletedAt: completedSession.CompletedAt ?? DateTime.UtcNow
        );
    }
}
