namespace VendorMaster.Api.Endpoints;

// wrapper request buat fleksibilitas payload flat atau nested { "vendor": { ... } }
public class SaveVendorRequest : VendorDto
{
    public VendorDto? Vendor { get; set; }
    public VendorDto Resolve() => Vendor ?? this;
}

public class VendorEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // routing resmi v1 vendor telematika
        var group = app.MapGroup("/api/v1/vendors")
            .WithTags("Vendors");

        // GET /api/v1/vendors
        group.MapGet("/", async ([AsParameters] GetVendorsDto queryParams, ISender sender) =>
        {
            var result = await sender.Send(new GetVendorsQuery(queryParams));
            return Results.Ok(result.Vendors);
        })
        .WithName("GetVendors")
        .Produces<PaginatedResult<VendorDto>>(StatusCodes.Status200OK)
        .WithSummary("Ambil daftar vendor GPS terpaginasi");

        // GET /api/v1/vendors/{id}
        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetVendorByIdQuery(id));
            return Results.Ok(result.Vendor);
        })
        .WithName("GetVendorById")
        .Produces<VendorDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Ambil detail vendor GPS by Id");

        // POST /api/v1/vendors
        group.MapPost("/", async (SaveVendorRequest request, ISender sender) =>
        {
            var dto = request.Resolve();
            var result = await sender.Send(new CreateVendorCommand(dto));
            return Results.Created($"/api/v1/vendors/{result.Id}", result);
        })
        .RequireIdempotency()
        .WithName("CreateVendor")
        .Produces<CreateVendorResult>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .WithSummary("Daftarkan konfigurasi vendor GPS baru")
        .WithOpenApi(operation =>
        {
            operation.Parameters.Add(new Microsoft.OpenApi.Models.OpenApiParameter
            {
                Name = "X-Idempotency-Key",
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                Required = true,
                Description = "Kunci idempotensi (UUID) untuk mencegah pendaftaran duplikat dari network retry.",
                Schema = new Microsoft.OpenApi.Models.OpenApiSchema { Type = "string", Format = "uuid" }
            });
            return operation;
        });

        // PUT /api/v1/vendors
        group.MapPut("/", async (SaveVendorRequest request, ISender sender) =>
        {
            var dto = request.Resolve();
            var result = await sender.Send(new UpdateVendorCommand(dto));
            return Results.Ok(result);
        })
        .WithName("UpdateVendor")
        .Produces<UpdateVendorResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .WithSummary("Update konfigurasi vendor GPS");

        // DELETE /api/v1/vendors/{id}
        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteVendorCommand(id));
            return Results.Ok(result);
        })
        .WithName("DeleteVendor")
        .Produces<DeleteVendorResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Hapus vendor GPS");

        // GET /api/v1/vendors/active
        group.MapGet("/active", async (
            StackExchange.Redis.IConnectionMultiplexer? redis,
            IVendorCacheSyncService cacheSync,
            IVendorMasterRepository repo,
            CancellationToken ct) =>
        {
            if (redis != null && redis.IsConnected)
            {
                var db = redis.GetDatabase();
                var cachedJson = await db.StringGetAsync(Shared.Data.Cache.VendorCacheConstants.ActiveVendorsCacheKey);
                if (cachedJson.HasValue && !cachedJson.IsNullOrEmpty)
                {
                    var cachedVendors = System.Text.Json.JsonSerializer.Deserialize<List<Shared.Data.Cache.ActiveVendorCacheDto>>(
                        cachedJson.ToString(),
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (cachedVendors is { Count: > 0 })
                    {
                        return Results.Ok(new { Source = "RedisCache", Data = cachedVendors });
                    }
                }
            }

            // Fallback ke PostgreSQL & rehydrate Redis
            await cacheSync.RefreshActiveVendorsCacheAsync(ct);
            var vendors = await repo.GetVendorsAsync(null, null, null, 1, 100, true, ct);
            return Results.Ok(new { Source = "PostgreSQL", Data = vendors });
        })
        .WithName("GetActiveVendors")
        .WithSummary("Ambil seluruh konfigurasi vendor aktif dari Redis (atau fallback ke PostgreSQL)");

        // POST /api/v1/vendors/cache/refresh
        group.MapPost("/cache/refresh", async (IVendorCacheSyncService cacheSync, CancellationToken ct) =>
        {
            await cacheSync.RefreshActiveVendorsCacheAsync(ct);
            return Results.Ok(new { Message = "Cache vendor aktif di Redis berhasil di-refresh dan disimpan selamanya." });
        })
        .WithName("RefreshActiveVendorsCache")
        .WithSummary("Paksa sinkronisasi ulang data vendor aktif ke Redis cache (hapus & isi ulang)");
    }
}
