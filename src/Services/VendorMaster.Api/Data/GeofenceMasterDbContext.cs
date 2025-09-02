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
    public DbSet<Msystem> Msystems => Set<Msystem>();
    
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
            
            entity.Property(e => e.CreatedAt)
                . HasColumnType("timestamp without time zone");
            entity.Property(e => e.LastModified)
                . HasColumnType("timestamp without time zone");
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
        
        builder.Entity<GpsVendorLpcd>(entity =>
        {
            entity.ToTable("tb_m_gps_vendor_lpcd");
            entity.Property(e => e.CreatedAt)
                . HasColumnType("timestamp without time zone");
            entity.Property(e => e.LastModified)
                . HasColumnType("timestamp without time zone");
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
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());        
        base.OnModelCreating(builder);
        
        
    }
}