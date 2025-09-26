using VendorMaster.Infrastructure.Converters;

namespace VendorMaster.Infrastructure.Persistence.Configurations;

// konfigurasi entity framework core untuk tabel-tabel vendor master (schema edcl)

public class GpsVendorConfiguration : IEntityTypeConfiguration<GpsVendor>
{
    public void Configure(EntityTypeBuilder<GpsVendor> builder)
    {
        builder.ToTable("m_gps_vendor", "edcl");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.VendorCode).HasColumnName("vendor_code").HasMaxLength(50).IsRequired();
        builder.Property(e => e.VendorName).HasColumnName("vendor_name").HasMaxLength(150).IsRequired();
        builder.Property(e => e.ProtocolType).HasColumnName("protocol_type").HasMaxLength(50).HasDefaultValue(VendorDefaults.DefaultProtocolType);
        builder.Property(e => e.Timezone).HasColumnName("timezone").HasMaxLength(50).HasDefaultValue(VendorDefaults.DefaultTimezone);
        builder.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(e => e.Version).HasColumnName("version").HasDefaultValue(VendorDefaults.DefaultVersion);
        builder.Property(e => e.ProcessingStrategy).HasColumnName("processing_strategy").HasMaxLength(50).HasDefaultValue(VendorDefaults.DefaultProcessingStrategy);
        builder.Property(e => e.ProcessingStrategyPathData).HasColumnName("processing_strategy_path_data").HasMaxLength(255);
        builder.Property(e => e.ProcessingStrategyPathKey).HasColumnName("processing_strategy_path_key").HasMaxLength(255);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp without time zone");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").HasMaxLength(50);
        builder.Property(e => e.LastModified).HasColumnName("last_modified").HasColumnType("timestamp without time zone");
        builder.Property(e => e.LastModifiedBy).HasColumnName("last_modified_by").HasMaxLength(50);
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        // filter soft delete
        builder.HasQueryFilter(e => !e.IsDeleted);

        // relasi ke child entities
        builder.HasMany(e => e.GpsVendorEndpoints)
            .WithOne(e => e.GpsVendor)
            .HasForeignKey(e => e.VendorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.GpsVendorAuth)
            .WithOne(e => e.GpsVendor)
            .HasForeignKey<GpsVendorAuth>(e => e.VendorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Mappings)
            .WithOne(e => e.GpsVendor)
            .HasForeignKey(e => e.VendorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Lpcds)
            .WithOne(e => e.GpsVendor)
            .HasForeignKey(e => e.VendorId)
            .OnDelete(DeleteBehavior.Cascade);

        // mapping langsung ke private backing field agar materialisasi ef core aman
        builder.Navigation(e => e.GpsVendorEndpoints)
            .HasField("_endpoints")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(e => e.Mappings)
            .HasField("_mappings")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(e => e.Lpcds)
            .HasField("_lpcds")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class GpsVendorEndpointConfiguration : IEntityTypeConfiguration<GpsVendorEndpoint>
{
    public void Configure(EntityTypeBuilder<GpsVendorEndpoint> builder)
    {
        builder.ToTable("m_gps_vendor_endpoint", "edcl");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.VendorId).HasColumnName("vendor_id").IsRequired();
        builder.Property(e => e.EndpointType).HasColumnName("endpoint_type").HasMaxLength(50).HasDefaultValue(VendorDefaults.DefaultEndpointType);
        builder.Property(e => e.BaseUrl).HasColumnName("base_url").HasMaxLength(500).IsRequired();
        builder.Property(e => e.Method).HasColumnName("method").HasMaxLength(10).HasDefaultValue(VendorDefaults.DefaultHttpMethod);
        builder.Property(e => e.ContentType).HasColumnName("content_type").HasMaxLength(50).HasDefaultValue(VendorDefaults.DefaultContentType);

        builder.Property(e => e.Headers)
            .HasColumnName("headers")
            .HasConversion(new JsonObjectValueConverter())
            .HasColumnType("jsonb");

        builder.Property(e => e.Params)
            .HasColumnName("params")
            .HasConversion(new JsonObjectValueConverter())
            .HasColumnType("jsonb");

        builder.Property(e => e.Bodies)
            .HasColumnName("bodies")
            .HasConversion(new JsonObjectValueConverter())
            .HasColumnType("jsonb");

        builder.Property(e => e.VarParams)
            .HasColumnName("var_params")
            .HasConversion(new JsonObjectValueConverter())
            .HasColumnType("jsonb");

        builder.Property(e => e.MaxPath).HasColumnName("max_path").HasMaxLength(255);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp without time zone");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").HasMaxLength(50);
        builder.Property(e => e.LastModified).HasColumnName("last_modified").HasColumnType("timestamp without time zone");
        builder.Property(e => e.LastModifiedBy).HasColumnName("last_modified_by").HasMaxLength(50);
    }
}

public class GpsVendorAuthConfiguration : IEntityTypeConfiguration<GpsVendorAuth>
{
    public void Configure(EntityTypeBuilder<GpsVendorAuth> builder)
    {
        builder.ToTable("m_gps_vendor_auth", "edcl");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.VendorId).HasColumnName("vendor_id").IsRequired();
        builder.Property(e => e.AuthType).HasColumnName("auth_type").HasMaxLength(50).HasDefaultValue(VendorDefaults.DefaultAuthType);
        builder.Property(e => e.BaseUrl).HasColumnName("base_url").HasMaxLength(500).IsRequired();
        builder.Property(e => e.Method).HasColumnName("method").HasMaxLength(10).HasDefaultValue(VendorDefaults.DefaultAuthHttpMethod);
        builder.Property(e => e.ContentType).HasColumnName("content_type").HasMaxLength(50).HasDefaultValue(VendorDefaults.DefaultContentType);
        builder.Property(e => e.Username).HasColumnName("username").HasMaxLength(100);
        builder.Property(e => e.Password).HasColumnName("password").HasMaxLength(255);
        builder.Property(e => e.TokenPath).HasColumnName("token_path").HasMaxLength(255);
        builder.Property(e => e.TokenExpiresInPath).HasColumnName("token_expires_in_path").HasMaxLength(255);
        builder.Property(e => e.TokenTtlSeconds).HasColumnName("token_ttl_seconds").HasDefaultValue(VendorDefaults.DefaultTokenTtlSeconds);

        builder.Property(e => e.Headers)
            .HasColumnName("headers")
            .HasConversion(new JsonObjectValueConverter())
            .HasColumnType("jsonb");

        builder.Property(e => e.Params)
            .HasColumnName("params")
            .HasConversion(new JsonObjectValueConverter())
            .HasColumnType("jsonb");

        builder.Property(e => e.Bodies)
            .HasColumnName("bodies")
            .HasConversion(new JsonObjectValueConverter())
            .HasColumnType("jsonb");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp without time zone");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").HasMaxLength(50);
        builder.Property(e => e.LastModified).HasColumnName("last_modified").HasColumnType("timestamp without time zone");
        builder.Property(e => e.LastModifiedBy).HasColumnName("last_modified_by").HasMaxLength(50);
    }
}

public class GpsVendorMappingConfiguration : IEntityTypeConfiguration<GpsVendorMapping>
{
    public void Configure(EntityTypeBuilder<GpsVendorMapping> builder)
    {
        builder.ToTable("m_gps_vendor_mapping", "edcl");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.VendorId).HasColumnName("vendor_id").IsRequired();
        builder.Property(e => e.ResponseField).HasColumnName("response_field").HasMaxLength(100).IsRequired();
        builder.Property(e => e.MappedField).HasColumnName("mapped_field").HasMaxLength(100).IsRequired();
        builder.Property(e => e.DataType).HasColumnName("data_type").HasMaxLength(50).HasDefaultValue(VendorDefaults.DefaultMappingDataType);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp without time zone");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").HasMaxLength(50);
        builder.Property(e => e.LastModified).HasColumnName("last_modified").HasColumnType("timestamp without time zone");
        builder.Property(e => e.LastModifiedBy).HasColumnName("last_modified_by").HasMaxLength(50);
    }
}

public class GpsVendorLpcdConfiguration : IEntityTypeConfiguration<GpsVendorLpcd>
{
    public void Configure(EntityTypeBuilder<GpsVendorLpcd> builder)
    {
        builder.ToTable("m_gps_vendor_lpcd", "edcl");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.VendorId).HasColumnName("vendor_id").IsRequired();
        builder.Property(e => e.Lpcd).HasColumnName("lpcd").HasMaxLength(20).IsRequired();
        builder.HasIndex(e => e.Lpcd).IsUnique();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp without time zone");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").HasMaxLength(50);
        builder.Property(e => e.LastModified).HasColumnName("last_modified").HasColumnType("timestamp without time zone");
        builder.Property(e => e.LastModifiedBy).HasColumnName("last_modified_by").HasMaxLength(50);
    }
}

public class MsystemConfiguration : IEntityTypeConfiguration<Msystem>
{
    public void Configure(EntityTypeBuilder<Msystem> builder)
    {
        builder.ToTable("m_system", "edcl");

        builder.HasKey(e => new { e.SysCat, e.SysSubCat, e.SysCd });
        builder.Property(e => e.SysCat).HasColumnName("sys_cat").HasMaxLength(50);
        builder.Property(e => e.SysSubCat).HasColumnName("sys_sub_cat").HasMaxLength(50);
        builder.Property(e => e.SysCd).HasColumnName("sys_cd").HasMaxLength(50);
        builder.Property(e => e.SysValue).HasColumnName("sys_value").HasMaxLength(500).IsRequired();
        builder.Property(e => e.Remarks).HasColumnName("remarks").HasMaxLength(255);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp without time zone");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").HasMaxLength(50);
        builder.Property(e => e.LastModified).HasColumnName("last_modified").HasColumnType("timestamp without time zone");
        builder.Property(e => e.LastModifiedBy).HasColumnName("last_modified_by").HasMaxLength(50);
    }
}

public class GeofenceDestinationConfiguration : IEntityTypeConfiguration<GeofenceDestination>
{
    public void Configure(EntityTypeBuilder<GeofenceDestination> builder)
    {
        builder.ToTable("m_geofence_destination", "edcl");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.DestinationCode).HasColumnName("destination_code").HasMaxLength(50).IsRequired();
        builder.Property(e => e.DestinationName).HasColumnName("destination_name").HasMaxLength(200).IsRequired();
        builder.Property(e => e.GeofenceType).HasColumnName("geofence_type").HasMaxLength(20).HasDefaultValue(VendorDefaults.DefaultGeofenceType);
        builder.Property(e => e.CenterLatitude).HasColumnName("center_latitude").HasPrecision(19, 16).IsRequired();
        builder.Property(e => e.CenterLongitude).HasColumnName("center_longitude").HasPrecision(19, 16).IsRequired();
        builder.Property(e => e.RadiusMeters).HasColumnName("radius_meters").HasDefaultValue(VendorDefaults.DefaultGeofenceRadiusMeters);

        builder.Property(e => e.PolygonCoordinates)
            .HasColumnName("polygon_coordinates")
            .HasConversion(new JsonObjectValueConverter())
            .HasColumnType("jsonb");

        builder.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp without time zone");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").HasMaxLength(50);
        builder.Property(e => e.LastModified).HasColumnName("last_modified").HasColumnType("timestamp without time zone");
        builder.Property(e => e.LastModifiedBy).HasColumnName("last_modified_by").HasMaxLength(50);
    }
}
