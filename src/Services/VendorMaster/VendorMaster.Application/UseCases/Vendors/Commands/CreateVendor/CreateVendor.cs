namespace VendorMaster.Application.UseCases.Vendors.Commands.CreateVendor;

// request & result create vendor
public record CreateVendorCommand(VendorDto Vendor) : ICommand<CreateVendorResult>;
public record CreateVendorResult(Guid Id);

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
    ILogger<CreateVendorHandler> logger) : ICommandHandler<CreateVendorCommand, CreateVendorResult>
{
    public async Task<CreateVendorResult> Handle(CreateVendorCommand command, CancellationToken cancellationToken)
    {
        var dto = command.Vendor;

        // cek apakah vendor code udah dipake
        var existingVendor = await repository.GetVendorByCodeAsync(dto.VendorCode, cancellationToken: cancellationToken);
        if (existingVendor != null)
        {
            throw new VendorConflictException($"Vendor dengan kode '{dto.VendorCode}' sudah terdaftar.");
        }

        // cek duplikasi lpcd di payload request
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

        // pasang unit armada toyota (lpcd)
        foreach (var l in dto.Lpcds)
        {
            vendor.AddLpcd(l.Id, l.Lpcd);
        }

        // simpan ke database
        await repository.CreateVendorAsync(vendor, cancellationToken);
        logger.LogInformation("Berhasil mendaftarkan vendor baru: {VendorCode} ({Id})", vendor.VendorCode, vendor.Id);

        return new CreateVendorResult(vendor.Id);
    }
}
