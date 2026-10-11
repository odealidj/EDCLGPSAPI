namespace VendorMaster.Application.UseCases.Vendors.Commands.UpdateVendor;

// request & result update vendor
public record UpdateVendorCommand(VendorDto Vendor) : ICommand<UpdateVendorResult>;
public record UpdateVendorResult(bool IsSuccess, Guid Id = default, string VendorCode = "", string Lpcd = "");

// validasi input update vendor
public class UpdateVendorValidator : AbstractValidator<UpdateVendorCommand>
{
    public UpdateVendorValidator()
    {
        RuleFor(x => x.Vendor.Id).NotEmpty().WithMessage("Id vendor wajib diisi.");
        RuleFor(x => x.Vendor.VendorCode).NotEmpty().WithMessage("Kode vendor wajib diisi.");
        RuleFor(x => x.Vendor.VendorName).NotEmpty().WithMessage("Nama vendor wajib diisi.");

        RuleFor(x => x.Vendor.ProtocolType)
            .Must(x => string.IsNullOrWhiteSpace(x) || Enum.TryParse<ProtocolType>(x, true, out _))
            .WithMessage($"ProtocolType tidak valid. Pilihan: {string.Join(", ", Enum.GetNames<ProtocolType>())}");

        RuleFor(x => x.Vendor.ProcessingStrategy)
            .Must(x => string.IsNullOrWhiteSpace(x) || Enum.TryParse<ProcessingStrategy>(x, true, out _))
            .WithMessage($"ProcessingStrategy tidak valid. Pilihan: {string.Join(", ", Enum.GetNames<ProcessingStrategy>())}");

        RuleFor(x => x.Vendor.Lpcds)
            .Must((cmd, lpcds) => lpcds.Count > 0 || !string.IsNullOrWhiteSpace(cmd.Vendor.Lpcd))
            .WithMessage("LPCD wajib disertakan saat update vendor.");

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

// handler use case update konfigurasi vendor
public class UpdateVendorHandler(
    IVendorMasterRepository repository,
    ILogger<UpdateVendorHandler> logger,
    IVendorCacheSyncService? vendorCacheSync = null) : ICommandHandler<UpdateVendorCommand, UpdateVendorResult>
{
    public async Task<UpdateVendorResult> Handle(UpdateVendorCommand command, CancellationToken cancellationToken)
    {
        var dto = command.Vendor;

        SynchronizeAndValidateLpcds(dto);

        var vendor = await repository.GetVendorByIdAsync(dto.Id, asNoTracking: false, cancellationToken);
        if (vendor == null)
        {
            throw new VendorNotFoundException(dto.Id);
        }

        await ValidateVendorAsync(vendor, dto, cancellationToken);
        ValidateEndpoints(dto.Endpoints);

        vendor.UpdateDetails(
            dto.VendorCode,
            dto.VendorName,
            dto.ProtocolType,
            dto.Timezone,
            dto.IsActive,
            dto.ProcessingStrategy,
            dto.ProcessingStrategyPathData,
            dto.ProcessingStrategyPathKey);

        SyncEndpoints(vendor, dto.Endpoints);
        SyncAuth(vendor, dto.Auth);
        SyncMappings(vendor, dto.Mappings);

        var primaryLpcd = ResolvePrimaryLpcd(dto);
        vendor.SetLpcd(primaryLpcd);

        await repository.UpdateVendorAsync(vendor, cancellationToken);
        logger.LogInformation("Berhasil update vendor: {VendorCode} ({Id})", vendor.VendorCode, vendor.Id);

        if (vendorCacheSync != null)
        {
            await vendorCacheSync.RefreshActiveVendorsCacheAsync(cancellationToken);
        }

        return new UpdateVendorResult(true, vendor.Id, vendor.VendorCode, primaryLpcd);
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

    private async Task ValidateVendorAsync(GpsVendor vendor, VendorDto dto, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(dto.VendorCode) && 
            !string.Equals(dto.VendorCode, vendor.VendorCode, StringComparison.OrdinalIgnoreCase))
        {
            var codeOwner = await repository.GetVendorByCodeAsync(dto.VendorCode, cancellationToken: cancellationToken);
            if (codeOwner != null && codeOwner.Id != vendor.Id)
            {
                throw new VendorConflictException($"Vendor dengan kode '{dto.VendorCode}' sudah terdaftar pada vendor lain.");
            }
        }

        foreach (var lpcd in dto.Lpcds.Select(l => l.Lpcd))
        {
            var isExists = await repository.IsLpcdExistsAsync(lpcd, excludeVendorId: vendor.Id, cancellationToken: cancellationToken);
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

    private static void SyncEndpoints(GpsVendor vendor, List<VendorEndpointDto> endpoints)
    {
        var existingEndpoints = vendor.GpsVendorEndpoints.ToList();
        RemoveObsoleteEndpoints(vendor, existingEndpoints, endpoints);
        UpsertEndpoints(vendor, existingEndpoints, endpoints);
    }

    private static bool IsEndpointMatching(VendorEndpointDto dto, GpsVendorEndpoint existing) =>
        (dto.Id != Guid.Empty && dto.Id == existing.Id) ||
        (dto.Id == Guid.Empty && dto.EndpointType == existing.EndpointType);

    private static void RemoveObsoleteEndpoints(
        GpsVendor vendor,
        List<GpsVendorEndpoint> existingEndpoints,
        List<VendorEndpointDto> endpoints)
    {
        var endpointsToRemove = existingEndpoints
            .Where(ex => !endpoints.Any(n => IsEndpointMatching(n, ex)))
            .ToList();

        foreach (var rem in endpointsToRemove)
        {
            vendor.RemoveEndpoint(rem);
        }
    }

    private static void UpsertEndpoints(
        GpsVendor vendor,
        List<GpsVendorEndpoint> existingEndpoints,
        List<VendorEndpointDto> endpoints)
    {
        foreach (var ep in endpoints)
        {
            var existing = existingEndpoints.FirstOrDefault(ex => IsEndpointMatching(ep, ex));
            if (existing != null)
            {
                UpdateExistingEndpoint(existing, ep);
            }
            else
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
        }
    }

    private static void UpdateExistingEndpoint(GpsVendorEndpoint existing, VendorEndpointDto ep)
    {
        existing.EndpointType = string.IsNullOrWhiteSpace(ep.EndpointType) ? VendorDefaults.DefaultEndpointType : ep.EndpointType;
        existing.BaseUrl = ep.BaseUrl;
        existing.Method = string.IsNullOrWhiteSpace(ep.Method) ? VendorDefaults.DefaultHttpMethod : ep.Method;
        existing.ContentType = ep.ContentType ?? VendorDefaults.DefaultContentType;
        existing.Headers = ep.Headers;
        existing.Params = ep.Params;
        existing.Bodies = ep.Bodies;
        existing.VarParams = ep.VarParams;
        existing.MaxPath = ep.MaxPath;
    }

    private static void SyncAuth(GpsVendor vendor, VendorAuthDto? authDto)
    {
        if (authDto == null)
        {
            vendor.GpsVendorAuth = null;
            return;
        }

        if (vendor.GpsVendorAuth != null)
        {
            vendor.GpsVendorAuth.AuthType = string.IsNullOrWhiteSpace(authDto.Authtype) ? VendorDefaults.DefaultAuthType : authDto.Authtype;
            vendor.GpsVendorAuth.BaseUrl = authDto.BaseUrl;
            vendor.GpsVendorAuth.Method = string.IsNullOrWhiteSpace(authDto.Method) ? VendorDefaults.DefaultAuthHttpMethod : authDto.Method;
            vendor.GpsVendorAuth.ContentType = authDto.ContentType ?? VendorDefaults.DefaultContentType;
            vendor.GpsVendorAuth.Username = authDto.Username;
            vendor.GpsVendorAuth.Password = authDto.Password;
            vendor.GpsVendorAuth.TokenPath = authDto.TokenPath;
            vendor.GpsVendorAuth.TokenExpiresInPath = authDto.TokenExpiresInPath;
            vendor.GpsVendorAuth.TokenTtlSeconds = authDto.TokenTtlSeconds <= 0 ? VendorDefaults.DefaultTokenTtlSeconds : authDto.TokenTtlSeconds;
            vendor.GpsVendorAuth.Headers = authDto.Headers;
            vendor.GpsVendorAuth.Params = authDto.Params;
            vendor.GpsVendorAuth.Bodies = authDto.Bodies;
        }
        else
        {
            vendor.SetAuth(
                authDto.Id,
                authDto.Authtype,
                authDto.BaseUrl,
                authDto.Method,
                authDto.ContentType,
                authDto.Username,
                authDto.Password,
                authDto.TokenPath,
                authDto.TokenExpiresInPath,
                authDto.TokenTtlSeconds,
                authDto.Headers,
                authDto.Params,
                authDto.Bodies);
        }
    }

    private static void SyncMappings(GpsVendor vendor, List<VendorMappingDto> mappings)
    {
        var existingMappings = vendor.Mappings.ToList();
        RemoveObsoleteMappings(vendor, existingMappings, mappings);
        UpsertMappings(vendor, existingMappings, mappings);
    }

    private static bool IsMappingMatching(VendorMappingDto dto, GpsVendorMapping existing) =>
        (dto.Id > 0 && dto.Id == existing.Id) ||
        (dto.Id <= 0 && dto.ResponseField.Equals(existing.ResponseField, StringComparison.OrdinalIgnoreCase));

    private static void RemoveObsoleteMappings(
        GpsVendor vendor,
        List<GpsVendorMapping> existingMappings,
        List<VendorMappingDto> mappings)
    {
        var mappingsToRemove = existingMappings
            .Where(ex => !mappings.Any(n => IsMappingMatching(n, ex)))
            .ToList();

        foreach (var rem in mappingsToRemove)
        {
            vendor.RemoveMapping(rem);
        }
    }

    private static void UpsertMappings(
        GpsVendor vendor,
        List<GpsVendorMapping> existingMappings,
        List<VendorMappingDto> mappings)
    {
        foreach (var m in mappings)
        {
            var existing = existingMappings.FirstOrDefault(ex => IsMappingMatching(m, ex));
            if (existing != null)
            {
                existing.ResponseField = m.ResponseField;
                existing.MappedField = m.MappedField;
                existing.DataType = string.IsNullOrWhiteSpace(m.DataType) ? VendorDefaults.DefaultMappingDataType : m.DataType.ToUpperInvariant();
            }
            else
            {
                vendor.AddMapping(m.Id, m.ResponseField, m.MappedField, m.DataType);
            }
        }
    }

    private static string ResolvePrimaryLpcd(VendorDto dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.Lpcd))
        {
            return dto.Lpcd.Trim().ToUpperInvariant();
        }

        var first = dto.Lpcds.FirstOrDefault()?.Lpcd;
        return !string.IsNullOrWhiteSpace(first) ? first.Trim().ToUpperInvariant() : string.Empty;
    }
}
