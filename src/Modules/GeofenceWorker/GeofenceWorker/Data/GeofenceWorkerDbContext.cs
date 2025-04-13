using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using GeofenceWorker.Data.JsonConverters;
using GeofenceWorker.Workers.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace GeofenceWorker.Data;

public class GeofenceWorkerDbContext: DbContext
{
    public GeofenceWorkerDbContext(DbContextOptions<GeofenceWorkerDbContext> options)
        : base(options) { }

    public DbSet<GpsVendor> GpsVendors => Set<GpsVendor>();
    public DbSet<GpsVendorEndpoint> GpsVendorEndpoints => Set<GpsVendorEndpoint>();
    
    public DbSet<GpsVendorAuth> GpsVendorAuths => Set<GpsVendorAuth>();

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
        

        
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());        
        base.OnModelCreating(builder);
        
        
    }
}