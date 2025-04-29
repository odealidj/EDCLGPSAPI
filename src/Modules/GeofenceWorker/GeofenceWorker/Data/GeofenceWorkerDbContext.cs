using System.Reflection;
using GeofenceWorker.Data.JsonConverters;
using GeofenceWorker.Workers.Models;
using Microsoft.EntityFrameworkCore;

namespace GeofenceWorker.Data;

public class GeofenceWorkerDbContext: DbContext
{
    public GeofenceWorkerDbContext(DbContextOptions<GeofenceWorkerDbContext> options)
        : base(options) { }

    public DbSet<GpsVendor> GpsVendors => Set<GpsVendor>();
    public DbSet<GpsVendorEndpoint> GpsVendorEndpoints => Set<GpsVendorEndpoint>();
    
    public DbSet<GpsVendorAuth> GpsVendorAuths => Set<GpsVendorAuth>();
    public DbSet<Mapping> Mappings => Set<Mapping>();
    
    ////public DbSet<GpsVendorLpcd> Lpcds => Set<GpsVendorLpcd>();

    public DbSet<GpsLastPositionH> GpsLastPositionHs => Set<GpsLastPositionH>();
    public DbSet<GpsLastPositionD> GpsLastPositionDs => Set<GpsLastPositionD>();

    public DbSet<GpsDelivery> GpsDeliveries => Set<GpsDelivery>();

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
        });

        
        builder.Entity<GpsVendorAuth>(entity =>
        {
            entity.ToTable("tb_m_gps_vendor_auth");

            entity
                .Property(e => e.Headers)
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
        
        //builder.Entity<GpsVendorLpcd>().ToTable("tb_m_gps_vendor_lpcd");

        builder.Entity<GpsLastPositionH>().ToTable("tb_r_gps_last_position_h");
        builder.Entity<GpsLastPositionD>().ToTable("tb_r_gps_last_position_d");
        builder.Entity<GpsDelivery>().ToTable("tb_r_gps_delivery");
        
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());        
        base.OnModelCreating(builder);
        
        
    }
}