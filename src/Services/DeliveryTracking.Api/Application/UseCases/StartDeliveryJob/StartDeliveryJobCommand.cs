using System.Text.Json;
using DeliveryTracking.Application.DTOs;
using DeliveryTracking.Application.Ports;
using DeliveryTracking.Domain.Entities;
using DeliveryTracking.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Npgsql;
using Shared.Contracts.CQRS;
using StackExchange.Redis;

namespace DeliveryTracking.Application.UseCases.StartDeliveryJob;

public record StartDeliveryJobCommand(StartDeliveryJobRequestDto Request)
    : ICommand<StartDeliveryJobResultDto>;

public class StartDeliveryJobCommandValidator : AbstractValidator<StartDeliveryJobCommand>
{
    public StartDeliveryJobCommandValidator()
    {
        RuleFor(x => x.Request.DeliveryNo)
            .NotEmpty().WithMessage("Nomor Delivery / DO (deliveryNo) tidak boleh kosong.");

        RuleFor(x => x.Request.Lpcd)
            .NotEmpty().WithMessage("Kode LPCD transporter (lpcd) tidak boleh kosong.");

        RuleFor(x => x.Request.GetEffectiveValue())
            .NotEmpty().WithMessage("Nilai pencocokan (val / value) tidak boleh kosong.");

        RuleFor(x => x.Request.Key)
            .Must(DeliveryValidator.IsValidKey)
            .WithMessage("Kunci pencocokan (key) tidak valid. Kunci yang diizinkan: PlatNo, VehicleId, Imei, DriverKtp, DeliveryNo.");
    }
}

public class StartDeliveryJobCommandHandler(
    IDeliveryTrackingRepository repository,
    NpgsqlDataSource dataSource,
    IConnectionMultiplexer redis,
    ILogger<StartDeliveryJobCommandHandler> logger)
    : ICommandHandler<StartDeliveryJobCommand, StartDeliveryJobResultDto>
{
    public async Task<StartDeliveryJobResultDto> Handle(
        StartDeliveryJobCommand command,
        CancellationToken cancellationToken)
    {
        var req = command.Request;
        var effectiveValue = req.GetEffectiveValue();
        var cleanValue = DeliveryValidator.CleanKey(effectiveValue);
        var deliveryNo = req.DeliveryNo.Trim().ToUpperInvariant();
        var lpcd = req.Lpcd.Trim().ToUpperInvariant();
        var key = string.IsNullOrWhiteSpace(req.Key) ? "PlatNo" : req.Key.Trim();

        // 1. Resolve Vendor ID & Name dari tabel master edcl.m_gps_vendor_lpcd & edcl.m_gps_vendor
        Guid? vendorId = null;
        string? vendorName = null;

        const string sqlVendorByLpcd = @"
            SELECT v.id, v.vendor_name
            FROM edcl.m_gps_vendor_lpcd vl
            INNER JOIN edcl.m_gps_vendor v ON vl.vendor_id = v.id
            WHERE UPPER(vl.lpcd) = $1 AND v.is_active = true AND v.is_deleted = false
            LIMIT 1;";
        await using var cmdLpcd = dataSource.CreateCommand(sqlVendorByLpcd);
        cmdLpcd.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Varchar, lpcd);
        await using var readerLpcd = await cmdLpcd.ExecuteReaderAsync(cancellationToken);
        if (await readerLpcd.ReadAsync(cancellationToken))
        {
            vendorId = readerLpcd.GetGuid(0);
            vendorName = readerLpcd.GetString(1);
        }
        else
        {
            // Fallback 1: cari langsung dari tabel m_gps_vendor berdasarkan vendor_code
            const string sqlVendor = @"
                SELECT id, vendor_name
                FROM edcl.m_gps_vendor
                WHERE UPPER(vendor_code) = $1 AND is_active = true AND is_deleted = false
                LIMIT 1;";
            await using var cmdVendor = dataSource.CreateCommand(sqlVendor);
            cmdVendor.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Varchar, lpcd);
            await using var readerVendor = await cmdVendor.ExecuteReaderAsync(cancellationToken);
            if (await readerVendor.ReadAsync(cancellationToken))
            {
                vendorId = readerVendor.GetGuid(0);
                vendorName = readerVendor.GetString(1);
            }
        }

        if (vendorId == null)
        {
            logger.LogWarning("Start Job ditolak: Transporter dengan LPCD '{Lpcd}' tidak terdaftar atau non-aktif", lpcd);
            throw new DeliveryProgressBadRequestException($"Transporter / Vendor dengan LPCD atau kode '{lpcd}' tidak terdaftar atau non-aktif.");
        }

        // 2. Simpan manifes sesi ke database PostgreSQL (edcl.t_delivery_tracking_session)
        var session = DeliveryTrackingSession.Create(
            deliveryNo: deliveryNo,
            lpcd: lpcd,
            key: key,
            value: effectiveValue,
            cleanValue: cleanValue,
            vendorId: vendorId,
            driverKtp: req.DriverKtp
        );

        var savedSession = await repository.StartDeliverySessionAsync(session, cancellationToken);

        // 3. Daftarkan Hot Index ke Redis RAM (O(1) in-memory correlation untuk GpsCollector)
        try
        {
            var db = redis.GetDatabase();

            // Hash 1: lookup per LPCD transporter (delivery:active:lookup:AJL -> B9001FXR = DELIV-001)
            var lpcdHashKey = $"delivery:active:lookup:{lpcd}";
            await db.HashSetAsync(lpcdHashKey, cleanValue, deliveryNo);

            // Hash 2: lookup global aktif (delivery:active:lookup -> B9001FXR = DELIV-001)
            await db.HashSetAsync("delivery:active:lookup", cleanValue, deliveryNo);

            // Set: daftar delivery aktif
            await db.SetAddAsync("delivery:active:set", deliveryNo);

            // String cache detail sesi aktif (TTL 24 jam)
            var sessionPayload = JsonSerializer.Serialize(new
            {
                deliveryNo,
                lpcd,
                key,
                val = effectiveValue,
                cleanValue,
                vendorId,
                vendorName,
                driverKtp = req.DriverKtp,
                status = "STARTED",
                startedAt = savedSession.StartedAt
            });
            await db.StringSetAsync($"delivery:session:{deliveryNo}", sessionPayload, TimeSpan.FromHours(24));
            await db.StringSetAsync($"delivery:active:{cleanValue}", sessionPayload, TimeSpan.FromHours(24));

            logger.LogInformation("Hot index Redis berhasil didaftarkan: LPCD {Lpcd}, Value {CleanValue} -> Delivery {DeliveryNo}",
                lpcd, cleanValue, deliveryNo);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Gagal memperbarui cache/index Redis untuk delivery '{DeliveryNo}'. Database PostgreSQL tetap tersimpan.", deliveryNo);
        }

        logger.LogInformation("Sesi Start Job berhasil dimulai: Delivery '{DeliveryNo}', LPCD '{Lpcd}', CleanVal '{CleanValue}', Vendor '{VendorName}'",
            deliveryNo, lpcd, cleanValue, vendorName);

        return new StartDeliveryJobResultDto(
            Status: 200,
            Message: "Delivery tracking job started successfully",
            DeliveryNo: deliveryNo,
            Lpcd: lpcd,
            Key: key,
            Val: effectiveValue,
            CleanValue: cleanValue,
            VendorId: vendorId,
            VendorName: vendorName,
            StartedAt: savedSession.StartedAt
        );
    }
}
