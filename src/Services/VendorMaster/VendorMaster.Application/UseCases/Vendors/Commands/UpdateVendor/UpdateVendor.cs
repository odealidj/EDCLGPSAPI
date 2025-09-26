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
    ILogger<UpdateVendorHandler> logger) : ICommandHandler<UpdateVendorCommand, UpdateVendorResult>
{
    public async Task<UpdateVendorResult> Handle(UpdateVendorCommand command, CancellationToken cancellationToken)
    {
        var dto = command.Vendor;

        // sinkronisasi shortcut lpcd jika diisi string tunggal atau array string
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

        // load vendor existing beserta relasinya
        var vendor = await repository.GetVendorByIdAsync(dto.Id, asNoTracking: false, cancellationToken);
        if (vendor == null)
        {
            throw new VendorNotFoundException(dto.Id);
        }

        // jika vendor code diganti, pastiin belum dipake vendor lain
        if (!string.IsNullOrWhiteSpace(dto.VendorCode) && 
            !string.Equals(dto.VendorCode, vendor.VendorCode, StringComparison.OrdinalIgnoreCase))
        {
            var codeOwner = await repository.GetVendorByCodeAsync(dto.VendorCode, cancellationToken: cancellationToken);
            if (codeOwner != null && codeOwner.Id != vendor.Id)
            {
                throw new VendorConflictException($"Vendor dengan kode '{dto.VendorCode}' sudah terdaftar pada vendor lain.");
            }
        }

        // cek duplikasi lpcd
        var duplicateLpcds = dto.Lpcds
            .GroupBy(x => x.Lpcd.Trim().ToUpperInvariant())
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateLpcds.Count > 0)
        {
            throw new VendorBadRequestException($"Duplikat LPCD ditemukan: {string.Join(", ", duplicateLpcds)}");
        }

        // cek keunikan lpcd terhadap database (kecualikan vendor saat ini)
        foreach (var l in dto.Lpcds)
        {
            var isExists = await repository.IsLpcdExistsAsync(l.Lpcd, excludeVendorId: vendor.Id, cancellationToken: cancellationToken);
            if (isExists)
            {
                throw new VendorConflictException($"LPCD '{l.Lpcd.Trim().ToUpperInvariant()}' sudah terdaftar pada vendor lain.");
            }
        }

        // cek duplikasi endpoint base url
        var duplicateEndpoints = dto.Endpoints
            .GroupBy(x => x.BaseUrl.Trim().ToLowerInvariant())
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateEndpoints.Count > 0)
        {
            throw new VendorBadRequestException($"Duplikat BaseUrl endpoint ditemukan: {string.Join(", ", duplicateEndpoints)}");
        }

        // update data induk
        vendor.UpdateDetails(
            dto.VendorCode,
            dto.VendorName,
            dto.ProtocolType,
            dto.Timezone,
            dto.IsActive,
            dto.ProcessingStrategy,
            dto.ProcessingStrategyPathData,
            dto.ProcessingStrategyPathKey);

        // 1. Sinkronisasi endpoints secara in-place agar EF Core change tracker akurat
        var existingEndpoints = vendor.GpsVendorEndpoints.ToList();
        var endpointsToRemove = existingEndpoints.Where(ex =>
            !dto.Endpoints.Any(n => (n.Id != Guid.Empty && n.Id == ex.Id) ||
                                   (n.Id == Guid.Empty && n.EndpointType == ex.EndpointType))).ToList();
        foreach (var rem in endpointsToRemove)
        {
            vendor.RemoveEndpoint(rem);
        }

        foreach (var ep in dto.Endpoints)
        {
            var existing = existingEndpoints.FirstOrDefault(ex =>
                (ep.Id != Guid.Empty && ex.Id == ep.Id) ||
                (ep.Id == Guid.Empty && ex.EndpointType == ep.EndpointType));

            if (existing != null)
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

        // 2. Update / pasang auth config baru
        if (dto.Auth != null)
        {
            if (vendor.GpsVendorAuth != null)
            {
                vendor.GpsVendorAuth.AuthType = string.IsNullOrWhiteSpace(dto.Auth.Authtype) ? VendorDefaults.DefaultAuthType : dto.Auth.Authtype;
                vendor.GpsVendorAuth.BaseUrl = dto.Auth.BaseUrl;
                vendor.GpsVendorAuth.Method = string.IsNullOrWhiteSpace(dto.Auth.Method) ? VendorDefaults.DefaultAuthHttpMethod : dto.Auth.Method;
                vendor.GpsVendorAuth.ContentType = dto.Auth.ContentType ?? VendorDefaults.DefaultContentType;
                vendor.GpsVendorAuth.Username = dto.Auth.Username;
                vendor.GpsVendorAuth.Password = dto.Auth.Password;
                vendor.GpsVendorAuth.TokenPath = dto.Auth.TokenPath;
                vendor.GpsVendorAuth.TokenExpiresInPath = dto.Auth.TokenExpiresInPath;
                vendor.GpsVendorAuth.TokenTtlSeconds = dto.Auth.TokenTtlSeconds <= 0 ? VendorDefaults.DefaultTokenTtlSeconds : dto.Auth.TokenTtlSeconds;
                vendor.GpsVendorAuth.Headers = dto.Auth.Headers;
                vendor.GpsVendorAuth.Params = dto.Auth.Params;
                vendor.GpsVendorAuth.Bodies = dto.Auth.Bodies;
            }
            else
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
        }
        else
        {
            vendor.GpsVendorAuth = null;
        }

        // 3. Sinkronisasi mapping field secara in-place
        var existingMappings = vendor.Mappings.ToList();
        var mappingsToRemove = existingMappings.Where(ex =>
            !dto.Mappings.Any(n => (n.Id > 0 && n.Id == ex.Id) ||
                                   (n.Id <= 0 && n.ResponseField.Equals(ex.ResponseField, StringComparison.OrdinalIgnoreCase)))).ToList();
        foreach (var rem in mappingsToRemove)
        {
            vendor.RemoveMapping(rem);
        }

        foreach (var m in dto.Mappings)
        {
            var existing = existingMappings.FirstOrDefault(ex =>
                (m.Id > 0 && ex.Id == m.Id) ||
                (m.Id <= 0 && ex.ResponseField.Equals(m.ResponseField, StringComparison.OrdinalIgnoreCase)));

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

        // pasang unit armada toyota (lpcd) - 1 vendor, 1 lpcd
        var primaryLpcd = !string.IsNullOrWhiteSpace(dto.Lpcd)
            ? dto.Lpcd.Trim().ToUpperInvariant()
            : (dto.Lpcds.FirstOrDefault()?.Lpcd?.Trim().ToUpperInvariant() ?? string.Empty);
        vendor.SetLpcd(primaryLpcd);

        // commit perubahan
        await repository.UpdateVendorAsync(vendor, cancellationToken);
        logger.LogInformation("Berhasil update vendor: {VendorCode} ({Id})", vendor.VendorCode, vendor.Id);

        return new UpdateVendorResult(true, vendor.Id, vendor.VendorCode, primaryLpcd);
    }
}
