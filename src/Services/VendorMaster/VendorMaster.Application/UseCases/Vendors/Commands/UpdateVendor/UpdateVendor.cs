namespace VendorMaster.Application.UseCases.Vendors.Commands.UpdateVendor;

// request & result update vendor
public record UpdateVendorCommand(VendorDto Vendor) : ICommand<UpdateVendorResult>;
public record UpdateVendorResult(bool IsSuccess);

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

        // bersihkan list anak lama
        vendor.ClearEndpoints();
        vendor.ClearMappings();

        // pasang endpoints yang baru
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

        // update / pasang auth config baru
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
        else
        {
            vendor.GpsVendorAuth = null;
        }

        // pasang mapping baru
        foreach (var m in dto.Mappings)
        {
            vendor.AddMapping(m.Id, m.ResponseField, m.MappedField, m.DataType);
        }

        // pasang unit armada toyota (lpcd) - 1 vendor, 1 lpcd
        var primaryLpcd = dto.Lpcd ?? dto.Lpcds.FirstOrDefault()?.Lpcd ?? string.Empty;
        vendor.SetLpcd(primaryLpcd);

        // commit perubahan
        await repository.UpdateVendorAsync(vendor, cancellationToken);
        logger.LogInformation("Berhasil update vendor: {VendorCode} ({Id})", vendor.VendorCode, vendor.Id);

        return new UpdateVendorResult(true);
    }
}
