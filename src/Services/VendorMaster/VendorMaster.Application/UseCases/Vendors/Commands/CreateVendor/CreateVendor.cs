namespace VendorMaster.Application.UseCases.Vendors.Commands.CreateVendor;

// request & result create vendor
public record CreateVendorCommand(VendorDto Vendor) : ICommand<CreateVendorResult>;
public record CreateVendorResult(Guid Id, string VendorCode = "", string Lpcd = "");

// validasi input create vendor
public class CreateVendorValidator : AbstractValidator<CreateVendorCommand>
{
    public CreateVendorValidator()
    {
        RuleFor(x => x.Vendor.VendorCode)
            .NotEmpty().WithMessage("Kode vendor wajib diisi.");

        RuleFor(x => x.Vendor.VendorName)
            .NotEmpty().WithMessage("Nama vendor wajib diisi.");

        RuleFor(x => x.Vendor.ProtocolType)
            .Must(x => string.IsNullOrWhiteSpace(x) || Enum.TryParse<ProtocolType>(x, true, out _))
            .WithMessage($"ProtocolType tidak valid. Pilihan: {string.Join(", ", Enum.GetNames<ProtocolType>())}");

        RuleFor(x => x.Vendor.ProcessingStrategy)
            .Must(x => string.IsNullOrWhiteSpace(x) || Enum.TryParse<ProcessingStrategy>(x, true, out _))
            .WithMessage($"ProcessingStrategy tidak valid. Pilihan: {string.Join(", ", Enum.GetNames<ProcessingStrategy>())}");

        RuleFor(x => x.Vendor.Lpcds)
            .Must((cmd, lpcds) => lpcds.Count > 0 || !string.IsNullOrWhiteSpace(cmd.Vendor.Lpcd))
            .WithMessage("LPCD wajib disertakan saat mendaftarkan vendor.");

        RuleForEach(x => x.Vendor.Lpcds).ChildRules(l =>
        {
            l.RuleFor(x => x.Lpcd).NotEmpty().WithMessage("Kode LPCD tidak boleh kosong.");
        });

        RuleForEach(x => x.Vendor.Endpoints).ChildRules(ep =>
        {
            ep.RuleFor(e => e.BaseUrl).NotEmpty().WithMessage("BaseUrl endpoint wajib diisi.");
            ep.RuleFor(e => e.EndpointType)
                .Must(x => string.IsNullOrWhiteSpace(x) || Enum.TryParse<EndpointType>(x, true, out _))
                .WithMessage($"EndpointType tidak valid. Pilihan: {string.Join(", ", Enum.GetNames<EndpointType>())}");
        });

        When(x => x.Vendor.Auth != null, () =>
        {
            RuleFor(x => x.Vendor.Auth!.Authtype)
                .Must(x => string.IsNullOrWhiteSpace(x) || Enum.TryParse<AuthType>(x, true, out _))
                .WithMessage($"AuthType tidak valid. Pilihan: {string.Join(", ", Enum.GetNames<AuthType>())}");
        });
    }
}

// handler use case pendaftaran vendor baru
public class CreateVendorHandler(
    IVendorMasterRepository repository,
    ILogger<CreateVendorHandler> logger,
    IVendorCacheSyncService? vendorCacheSync = null) : ICommandHandler<CreateVendorCommand, CreateVendorResult>
{
    public async Task<CreateVendorResult> Handle(CreateVendorCommand command, CancellationToken cancellationToken)
    {
        var dto = command.Vendor;

        SynchronizeAndValidateLpcds(dto);
        await ValidateVendorUniquenessAsync(dto, cancellationToken);
        ValidateEndpoints(dto.Endpoints);

        // buat entitas aggregate baru
        var vendor = GpsVendor.Create(
            dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id,
            dto.VendorCode,
            dto.VendorName,
            dto.ProtocolType,
            dto.Timezone,
            dto.IsActive,
            dto.ProcessingStrategy,
            dto.ProcessingStrategyPathData,
            dto.ProcessingStrategyPathKey);

        // tambah endpoints
        foreach (var ep in dto.Endpoints)
        {
            vendor.AddEndpoint(
                ep.Id,
                ep.EndpointType,
                ep.BaseUrl,
                ep.Method,
                ep.ContentType,
                ep.Headers,
                ep.Params,
                ep.Bodies,
                ep.VarParams,
                ep.MaxPath);
        }

        // pasang auth jika ada
        if (dto.Auth != null)
        {
            vendor.SetAuth(
                dto.Auth.Id,
                dto.Auth.Authtype,
                dto.Auth.BaseUrl,
                dto.Auth.Method,
                dto.Auth.ContentType,
                dto.Auth.Username,
                dto.Auth.Password,
                dto.Auth.TokenPath,
                dto.Auth.TokenExpiresInPath,
                dto.Auth.TokenTtlSeconds,
                dto.Auth.Headers,
                dto.Auth.Params,
                dto.Auth.Bodies);
        }

        // pasang mapping json vendor -> model standar
        foreach (var m in dto.Mappings)
        {
            vendor.AddMapping(0, m.ResponseField, m.MappedField, m.DataType);
        }

        // pasang unit armada toyota (lpcd) - 1 vendor, 1 lpcd
        var primaryLpcd = !string.IsNullOrWhiteSpace(dto.Lpcd)
            ? dto.Lpcd.Trim().ToUpperInvariant()
            : (dto.Lpcds.Count > 0 ? dto.Lpcds[0].Lpcd?.Trim().ToUpperInvariant() ?? string.Empty : string.Empty);
        vendor.SetLpcd(primaryLpcd);

        // simpan ke database
        await repository.CreateVendorAsync(vendor, cancellationToken);
        logger.LogInformation("Berhasil mendaftarkan vendor baru: {VendorCode} ({Id})", vendor.VendorCode, vendor.Id);

        // sinkronisasi update/insert ke redis cache vendor aktif
        if (vendorCacheSync != null)
        {
            await vendorCacheSync.RefreshActiveVendorsCacheAsync(cancellationToken);
        }

        return new CreateVendorResult(vendor.Id, vendor.VendorCode, primaryLpcd);
    }

    private static void SynchronizeAndValidateLpcds(VendorDto dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.Lpcd) && !dto.Lpcds.Any(x => x.Lpcd.Equals(dto.Lpcd.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            dto.Lpcds.Add(new VendorLpcdDto { Lpcd = dto.Lpcd.Trim().ToUpperInvariant() });
        }
        else if (string.IsNullOrWhiteSpace(dto.Lpcd) && dto.Lpcds.Count > 0)
        {
            dto.Lpcd = dto.Lpcds[0].Lpcd.Trim().ToUpperInvariant();
        }

        if (dto.LpcdCodes is { Count: > 0 })
        {
            foreach (var code in dto.LpcdCodes)
            {
                if (!string.IsNullOrWhiteSpace(code) && !dto.Lpcds.Any(x => x.Lpcd.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    dto.Lpcds.Add(new VendorLpcdDto { Lpcd = code.Trim().ToUpperInvariant() });
                }
            }
        }

        if (dto.Lpcds.Count == 0)
        {
            throw new VendorBadRequestException("Vendor wajib memiliki minimal satu kode LPCD.");
        }

        var duplicateLpcds = dto.Lpcds
            .GroupBy(x => x.Lpcd.Trim().ToUpperInvariant())
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateLpcds.Count > 0)
        {
            throw new VendorBadRequestException($"Duplikat LPCD ditemukan: {string.Join(", ", duplicateLpcds)}");
        }
    }

    private async Task ValidateVendorUniquenessAsync(VendorDto dto, CancellationToken cancellationToken)
    {
        var existingVendor = await repository.GetVendorByCodeAsync(dto.VendorCode, cancellationToken: cancellationToken);
        if (existingVendor != null)
        {
            throw new VendorConflictException($"Vendor dengan kode '{dto.VendorCode}' sudah terdaftar.");
        }

        foreach (var lpcd in dto.Lpcds.Select(l => l.Lpcd))
        {
            var isExists = await repository.IsLpcdExistsAsync(lpcd, cancellationToken: cancellationToken);
            if (isExists)
            {
                throw new VendorConflictException($"LPCD '{lpcd.Trim().ToUpperInvariant()}' sudah terdaftar pada vendor lain.");
            }
        }
    }

    private static void ValidateEndpoints(IEnumerable<VendorEndpointDto> endpoints)
    {
        var duplicateEndpoints = endpoints
            .GroupBy(x => x.BaseUrl.Trim().ToLowerInvariant())
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateEndpoints.Count > 0)
        {
            throw new VendorBadRequestException($"Duplikat BaseUrl endpoint ditemukan: {string.Join(", ", duplicateEndpoints)}");
        }
    }
}
