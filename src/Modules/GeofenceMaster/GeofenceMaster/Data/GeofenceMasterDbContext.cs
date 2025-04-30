using GeofenceMaster.Data.JsonConverters;
using GeofenceMaster.GeofenceMaster.Models;

namespace GeofenceMaster.Data;

public class GeofenceMasterDbContext : DbContext
{
    public GeofenceMasterDbContext(DbContextOptions<GeofenceMasterDbContext> options)
        : base(options) { }

    public DbSet<GpsVendor> GpsVendors => Set<GpsVendor>();
    public DbSet<GpsVendorEndpoint> GpsVendorEndpoints => Set<GpsVendorEndpoint>();
    public DbSet<GpsVendorAuth> GpsVendorAuths => Set<GpsVendorAuth>();
    public DbSet<Mapping> Mappings => Set<Mapping>();
    
    public DbSet<GpsVendorLpcd> Lpcds => Set<GpsVendorLpcd>();
    
    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("edcl");
        
        builder.Entity<GpsVendor>().ToTable("tb_m_gps_vendor");
        
        builder.Entity<GpsVendorEndpoint>(entity =>
        {
            entity.ToTable("tb_m_gps_vendor_endpoint");

            entity.Property(e => e.Headers)
                .HasConversion(new JsonObjectValueConverter())
                .HasColumnType("jsonb");

            entity.Property(e => e.Params)
                .HasConversion(new JsonObjectValueConverter())
                .HasColumnType("jsonb");

            entity.Property(e => e.Bodies)
                .HasConversion(new JsonObjectValueConverter())
                .HasColumnType("jsonb");
            
            entity.Property(e => e.VarParams)
                .HasConversion(new JsonObjectValueConverter())
                .HasColumnType("jsonb");
        });

        builder.Entity<GpsVendorAuth>(entity =>
        {
            entity.ToTable("tb_m_gps_vendor_auth");

            entity.Property(e => e.Headers)
                .HasConversion(new JsonObjectValueConverter())
                .HasColumnType("jsonb");

            entity.Property(e => e.Params)
                .HasConversion(new JsonObjectValueConverter())
                .HasColumnType("jsonb");

            entity.Property(e => e.Bodies)
                .HasConversion(new JsonObjectValueConverter())
                .HasColumnType("jsonb");
        });
        
        builder.Entity<Mapping>().ToTable("tb_m_mapping");
        builder.Entity<GpsVendorLpcd>().ToTable("tb_m_gps_vendor_lpcd");
        
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());        
        base.OnModelCreating(builder);
        
        
    }
}