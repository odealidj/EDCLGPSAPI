namespace VendorMaster.Infrastructure.Persistence;

// dbcontext vendor master & geofence destination untuk postgresql (schema edcl)
public class VendorMasterDbContext : DbContext
{
    public VendorMasterDbContext(DbContextOptions<VendorMasterDbContext> options)
        : base(options) { }

    public DbSet<GpsVendor> GpsVendors => Set<GpsVendor>();
    public DbSet<GpsVendorEndpoint> GpsVendorEndpoints => Set<GpsVendorEndpoint>();
    public DbSet<GpsVendorAuth> GpsVendorAuths => Set<GpsVendorAuth>();
    public DbSet<GpsVendorMapping> Mappings => Set<GpsVendorMapping>();
    public DbSet<GpsVendorLpcd> Lpcds => Set<GpsVendorLpcd>();
    public DbSet<Msystem> Msystems => Set<Msystem>();
    public DbSet<GeofenceDestination> GeofenceDestinations => Set<GeofenceDestination>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("edcl");

        // scan semua entity configurations di assembly
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        base.OnModelCreating(builder);
    }
}
