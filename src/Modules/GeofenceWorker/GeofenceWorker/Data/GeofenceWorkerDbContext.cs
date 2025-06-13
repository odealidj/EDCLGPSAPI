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
    public DbSet<DeliveryProgress> DeliveryProgresses => Set<DeliveryProgress>();
    public DbSet<Msystem> Msystems => Set<Msystem>();
    
    public DbSet<GpsApiLog> GpsApiLogs => Set<GpsApiLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("edcl");

        builder.Entity<GpsVendor>(entity =>
        {
            entity.ToTable("tb_m_gps_vendor");
            entity.Property(e => e.CreatedAt)
                . HasColumnType("timestamp without time zone");
            entity.Property(e => e.LastModified)
                . HasColumnType("timestamp without time zone");
        });
        
        
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
            
            /*
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp with time zone")
                .HasConversion(
                    v => v, // Simpan DateTimeOffset secara langsung
                    v => v
                );
            */
            
            /*
            entity.Property(e => e.LastModified)
                .HasColumnType("timestamp with time zone")
                .IsRequired(false)
                .HasConversion(
                    v => v, // Simpan DateTimeOffset secara langsung
                    v => v
                );
            */
            
            entity.Property(e => e.CreatedAt)
                . HasColumnType("timestamp without time zone");
            
            entity.Property(e => e.LastModified)
                . HasColumnType("timestamp without time zone");
            
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
            
            entity.Property(e => e.CreatedAt)
                . HasColumnType("timestamp without time zone");
            entity.Property(e => e.LastModified)
                . HasColumnType("timestamp without time zone");
        });
        
        builder.Entity<Mapping>(entity =>
        {
            entity.ToTable("tb_m_mapping");
            entity.Property(e => e.CreatedAt)
                . HasColumnType("timestamp without time zone");
            entity.Property(e => e.LastModified)
                . HasColumnType("timestamp without time zone");
        });
        
        //builder.Entity<GpsVendorLpcd>().ToTable("tb_m_gps_vendor_lpcd");

        builder.Entity<GpsLastPositionH>(entity =>
        {
            entity.ToTable("tb_r_gps_last_position_h");
            ////entity.Property(e => e.CreatedAt)
                /*.HasConversion(
                    v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Unspecified) : (DateTime?)null,
                    v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Unspecified) : (DateTime?)null
                )*/
                ////. HasColumnType("timestamp without time zone");
                ////entity.Property(e => e.LastModified);
                /*.HasConversion(
                    v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Unspecified) : (DateTime?)null,
                    v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Unspecified) : (DateTime?)null
                )*/
                ////. HasColumnType("timestamp without time zone");

        });
        builder.Entity<GpsLastPositionD>(entity =>
        {
            entity.ToTable("tb_r_gps_last_position_d");
            entity.Property(e => e.Datetime)
                . HasColumnType("timestamp without time zone");
            ////entity.Property(e => e.CreatedAt)
            ////    . HasColumnType("timestamp without time zone");
            ////entity.Property(e => e.LastModified)
             ////   . HasColumnType("timestamp without time zone");

        });

        builder.Entity<Msystem>(entity => 
            {
                entity.ToTable("tb_m_system");
                entity.HasKey(e => new { e.SysCat, e.SysSubCat, e.SysCd });
                entity.Property(e=> e.SysCat).HasColumnName("SysCat");
                entity.Property(e => e.SysSubCat).HasColumnName("SysSubCat");
                entity.Property(e => e.SysCd).HasColumnName("SysCd");
                entity.Property(e => e.SysValue).HasColumnName("SysValue");
                entity.Property(e => e.Remarks).HasColumnName("Remarks");
                entity.Property(e => e.CreatedAt)
                    . HasColumnType("timestamp without time zone");
                entity.Property(e => e.LastModified)
                    . HasColumnType("timestamp without time zone");
            }    
        );
        
        builder.Entity<GpsDelivery>(entity =>
        {
            entity.ToTable("tb_r_gps_delivery");
            entity.Property(e => e.Datetime)
                . HasColumnType("timestamp without time zone");
            ////entity.Property(e => e.CreatedAt)
                ////. HasColumnType("timestamp without time zone");
            ////entity.Property(e => e.LastModified)
                ////. HasColumnType("timestamp without time zone");
        });

        builder.Entity<DeliveryProgress>(entity =>
        {
            entity.ToTable("tb_r_delivery_progress");
            ////entity.Property(e => e.CreatedAt)
            ////    .HasColumnType("timestamp without time zone");
            ////entity.Property(e => e.LastModified)
                ////. HasColumnType("timestamp without time zone");
        });
        
        builder.Entity<GpsApiLog>(entity =>
        {
            entity.ToTable("tb_m_gps_api_log");
            entity.Property(e => e.CreatedAt)
                . HasColumnType("timestamp with time zone");
            entity.Property(e => e.LastModified)
                . HasColumnType("timestamp with time zone");
        });
        
        
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());        
        base.OnModelCreating(builder);
        
        
    }
}