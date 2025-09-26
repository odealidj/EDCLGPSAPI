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
        vendor.ClearLpcds();

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

        // pasang lpcds baru
        foreach (var l in dto.Lpcds)
        {
            vendor.AddLpcd(l.Id, l.Lpcd);
        }

        // commit perubahan
        await repository.UpdateVendorAsync(vendor, cancellationToken);
        logger.LogInformation("Berhasil update vendor: {VendorCode} ({Id})", vendor.VendorCode, vendor.Id);

        return new UpdateVendorResult(true);
    }
}
