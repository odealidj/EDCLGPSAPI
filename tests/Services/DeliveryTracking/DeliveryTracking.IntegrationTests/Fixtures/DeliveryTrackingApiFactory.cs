namespace DeliveryTracking.IntegrationTests.Fixtures;

// factory buat bootstrap webapplicationfactory + postgres testcontainer (.net 10)
public class DeliveryTrackingApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    static DeliveryTrackingApiFactory()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("edclgps_delivery_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync()
    {
        // 1. nyalakan postgres container
        await _dbContainer.StartAsync();

        // 2. siapkan skema database & fungsi / sp postgres untuk tes
        await using var conn = new NpgsqlConnection(_dbContainer.GetConnectionString());
        await conn.OpenAsync();

        const string ddlSql = @"
            CREATE SCHEMA IF NOT EXISTS edcl;

            -- tabel delivery progress
            CREATE TABLE IF NOT EXISTS edcl.tb_r_delivery_progress (
                id UUID PRIMARY KEY,
                delivery_no VARCHAR(100) UNIQUE NOT NULL,
                plat_no VARCHAR(50) NOT NULL,
                no_ktp VARCHAR(50) NOT NULL,
                vendor_name VARCHAR(100),
                lpcd VARCHAR(50),
                created_at TIMESTAMP NOT NULL DEFAULT NOW(),
                created_by VARCHAR(50) DEFAULT 'System',
                last_modified TIMESTAMP DEFAULT NOW(),
                last_modified_by VARCHAR(50) DEFAULT 'System'
            );

            -- tabel telemetri gps delivery
            CREATE TABLE IF NOT EXISTS edcl.tb_r_gps_delivery (
                id UUID PRIMARY KEY,
                gps_vendor_id UUID,
                gps_vendor_name VARCHAR(100),
                gps_delivery_h_id UUID,
                lpcd_id UUID,
                lpcd VARCHAR(50),
                delivery_no VARCHAR(100),
                no_ktp VARCHAR(50),
                plat_no VARCHAR(50),
                device_id VARCHAR(50),
                datetime TIMESTAMP,
                x NUMERIC(18, 8),
                y NUMERIC(18, 8),
                speed NUMERIC(10, 2),
                course NUMERIC(10, 2),
                street_name VARCHAR(255),
                created_at TIMESTAMP DEFAULT NOW(),
                created_by VARCHAR(50) DEFAULT 'System',
                last_modified TIMESTAMP DEFAULT NOW(),
                last_modified_by VARCHAR(50) DEFAULT 'System'
            );

            -- master vendor gps
            CREATE TABLE IF NOT EXISTS edcl.m_gps_vendor (
                id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                vendor_code VARCHAR(50) UNIQUE NOT NULL,
                vendor_name VARCHAR(150) NOT NULL,
                protocol_type VARCHAR(50) NOT NULL DEFAULT 'REST_API',
                timezone VARCHAR(50) NOT NULL DEFAULT 'Asia/Jakarta',
                is_active BOOLEAN NOT NULL DEFAULT true,
                version INT NOT NULL DEFAULT 1,
                processing_strategy VARCHAR(50) NOT NULL DEFAULT 'Individual',
                processing_strategy_path_data VARCHAR(255) NULL,
                processing_strategy_path_key VARCHAR(255) NULL,
                created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP,
                created_by VARCHAR(50) DEFAULT 'System',
                last_modified TIMESTAMP WITHOUT TIME ZONE NULL,
                last_modified_by VARCHAR(50) NULL,
                is_deleted BOOLEAN NOT NULL DEFAULT false
            );

            -- master vendor lpcd
            CREATE TABLE IF NOT EXISTS edcl.m_gps_vendor_lpcd (
                id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                vendor_id UUID NOT NULL REFERENCES edcl.m_gps_vendor(id) ON DELETE CASCADE,
                lpcd VARCHAR(20) NOT NULL,
                created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP,
                created_by VARCHAR(50) DEFAULT 'System',
                last_modified TIMESTAMP WITHOUT TIME ZONE NULL,
                last_modified_by VARCHAR(50) NULL
            );

            -- delivery tracking session
            CREATE TABLE IF NOT EXISTS edcl.t_delivery_tracking_session (
                id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                delivery_no VARCHAR(50) UNIQUE NOT NULL,
                lpcd VARCHAR(20) NOT NULL,
                vendor_id UUID NULL REFERENCES edcl.m_gps_vendor(id),
                match_key VARCHAR(50) NOT NULL DEFAULT 'PlatNo',
                match_value VARCHAR(100) NOT NULL,
                clean_match_value VARCHAR(100) NOT NULL,
                driver_ktp VARCHAR(50) NULL,
                status VARCHAR(30) NOT NULL DEFAULT 'STARTED',
                started_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
                arrived_at TIMESTAMP WITHOUT TIME ZONE NULL,
                destination_geofence_id UUID NULL,
                completed_at TIMESTAMP WITHOUT TIME ZONE NULL,
                completed_by VARCHAR(50) NULL,
                completion_source VARCHAR(50) NULL,
                created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP,
                created_by VARCHAR(50) DEFAULT 'System',
                last_modified TIMESTAMP WITHOUT TIME ZONE NULL,
                last_modified_by VARCHAR(50) NULL,
                is_deleted BOOLEAN NOT NULL DEFAULT false
            );

            -- seed dummy vendor AJL jika belum ada
            INSERT INTO edcl.m_gps_vendor (id, vendor_code, vendor_name, protocol_type, timezone, is_active, version)
            VALUES ('11111111-1111-1111-1111-111111111111', 'AJL', 'PT Anugerah Jaya Logistic', 'REST_API', 'Asia/Jakarta', true, 1)
            ON CONFLICT (vendor_code) DO NOTHING;

            INSERT INTO edcl.m_gps_vendor_lpcd (vendor_id, lpcd)
            VALUES ('11111111-1111-1111-1111-111111111111', 'AJL')
            ON CONFLICT DO NOTHING;

            -- stored procedure 1 tracking koordinat gps
            CREATE OR REPLACE FUNCTION edcl.sp_edclgps1_get_track_delivery(p_delivery_no VARCHAR, p_density INT)
            RETURNS TABLE (
                positionid INT,
                deliveryno VARCHAR,
                noktp VARCHAR,
                lpcd VARCHAR,
                gpsvendor VARCHAR,
                flaggps VARCHAR,
                platno VARCHAR,
                deviceid VARCHAR,
                datetime TIMESTAMP,
                x NUMERIC,
                y NUMERIC,
                speed NUMERIC,
                course NUMERIC,
                streetname VARCHAR
            ) AS $$
            BEGIN
                RETURN QUERY
                SELECT 
                    1 AS positionid,
                    g.delivery_no AS deliveryno,
                    g.no_ktp AS noktp,
                    g.lpcd AS lpcd,
                    g.gps_vendor_name AS gpsvendor,
                    'OK'::VARCHAR AS flaggps,
                    g.plat_no AS platno,
                    g.device_id AS deviceid,
                    g.datetime AS datetime,
                    g.x AS x,
                    g.y AS y,
                    g.speed AS speed,
                    g.course AS course,
                    g.street_name AS streetname
                FROM edcl.tb_r_gps_delivery g
                WHERE g.delivery_no = p_delivery_no
                ORDER BY g.datetime ASC;
            END;
            $$ LANGUAGE plpgsql;

            -- stored procedure 2 fallback tracking
            CREATE OR REPLACE FUNCTION edcl.sp_edclgps2_get_track_delivery(p_delivery_no VARCHAR, p_density INT)
            RETURNS TABLE (
                positionid INT,
                deliveryno VARCHAR,
                noktp VARCHAR,
                lpcd VARCHAR,
                gpsvendor VARCHAR,
                flaggps VARCHAR,
                platno VARCHAR,
                deviceid VARCHAR,
                datetime TIMESTAMP,
                x NUMERIC,
                y NUMERIC,
                speed NUMERIC,
                course NUMERIC,
                streetname VARCHAR
            ) AS $$
            BEGIN
                RETURN QUERY
                SELECT 
                    1 AS positionid,
                    g.delivery_no AS deliveryno,
                    g.no_ktp AS noktp,
                    g.lpcd AS lpcd,
                    g.gps_vendor_name AS gpsvendor,
                    'OK'::VARCHAR AS flaggps,
                    g.plat_no AS platno,
                    g.device_id AS deviceid,
                    g.datetime AS datetime,
                    g.x AS x,
                    g.y AS y,
                    g.speed AS speed,
                    g.course AS course,
                    g.street_name AS streetname
                FROM edcl.tb_r_gps_delivery g
                WHERE g.delivery_no = p_delivery_no
                ORDER BY g.datetime ASC;
            END;
            $$ LANGUAGE plpgsql;

            -- mock function vendor lookup by lpcd
            CREATE OR REPLACE FUNCTION edcl.get_latest_gps_vendor_by_lpcd(p_lpcd VARCHAR)
            RETURNS TABLE (
                gpsvendorid UUID,
                gpsvendorname VARCHAR
            ) AS $$
            BEGIN
                RETURN QUERY
                SELECT 'a0000000-0000-0000-0000-000000000001'::UUID, 'PT Vendor Tracker'::VARCHAR;
            END;
            $$ LANGUAGE plpgsql;

            -- mock function vendor lookup by plat no
            CREATE OR REPLACE FUNCTION edcl.get_latest_gps_vendor_by_plat_no(p_plat_no VARCHAR)
            RETURNS TABLE (
                gpsvendorid UUID,
                vendorname VARCHAR
            ) AS $$
            BEGIN
                RETURN QUERY
                SELECT 'a0000000-0000-0000-0000-000000000001'::UUID, 'PT Vendor Tracker'::VARCHAR;
            END;
            $$ LANGUAGE plpgsql;

            -- mock function lpcd lookup by vendor name
            CREATE OR REPLACE FUNCTION edcl.get_latest_lpcd_by_gps_vendor_name(p_vendor_name VARCHAR)
            RETURNS TABLE (
                gpsvendorid UUID,
                lpcd VARCHAR
            ) AS $$
            BEGIN
                RETURN QUERY
                SELECT 'a0000000-0000-0000-0000-000000000001'::UUID, 'LPCD-TEST-001'::VARCHAR;
            END;
            $$ LANGUAGE plpgsql;

            -- mock function sp_edclgps1_get_last_positions
            CREATE OR REPLACE FUNCTION edcl.sp_edclgps1_get_last_positions(
                p_gps_vendor_id UUID,
                p_vendor_name VARCHAR,
                p_plat_no VARCHAR,
                p_device_id VARCHAR,
                p_datetime TIMESTAMP,
                p_created_at DATE,
                p_page_index INT,
                p_page_size INT
            )
            RETURNS TABLE (
                id UUID,
                gpsvendorid UUID,
                vendorname VARCHAR,
                platno VARCHAR,
                deviceid VARCHAR,
                datetime TIMESTAMP,
                x NUMERIC,
                y NUMERIC,
                speed NUMERIC,
                course NUMERIC,
                streetname VARCHAR,
                created_at TIMESTAMP
            ) AS $$
            BEGIN
                RETURN QUERY
                SELECT 
                    'b0000000-0000-0000-0000-000000000001'::UUID AS id,
                    COALESCE(p_gps_vendor_id, 'a0000000-0000-0000-0000-000000000001'::UUID) AS gpsvendorid,
                    'PT Vendor Tracker'::VARCHAR AS vendorname,
                    COALESCE(p_plat_no, 'B1234XYZ')::VARCHAR AS platno,
                    'DEV-001'::VARCHAR AS deviceid,
                    NOW()::TIMESTAMP AS datetime,
                    106.827153::NUMERIC AS x,
                    -6.175392::NUMERIC AS y,
                    45.5::NUMERIC AS speed,
                    180.0::NUMERIC AS course,
                    'Jl. Sudirman No. 1'::VARCHAR AS streetname,
                    NOW()::TIMESTAMP AS created_at;
            END;
            $$ LANGUAGE plpgsql;

            -- mock function sp_edclgps1_get_last_positions_count
            CREATE OR REPLACE FUNCTION edcl.sp_edclgps1_get_last_positions_count(
                p_gps_vendor_id UUID,
                p_vendor_name VARCHAR,
                p_plat_no VARCHAR,
                p_device_id VARCHAR,
                p_datetime TIMESTAMP,
                p_created_at DATE
            )
            RETURNS BIGINT AS $$
            BEGIN
                RETURN 1::BIGINT;
            END;
            $$ LANGUAGE plpgsql;
        ";

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = ddlSql;
        await cmd.ExecuteNonQueryAsync();
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.StopAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // pasang koneksi postgres test container
        builder.UseSetting("ConnectionStrings:Database", _dbContainer.GetConnectionString());
        builder.UseSetting("Database", _dbContainer.GetConnectionString());
        builder.UseSetting("EnableSwagger", "true");
    }

    // helper buat dapetin npgsql datasource container pas butuh assert query langsung
    public NpgsqlDataSource GetDataSource()
    {
        return Services.GetRequiredService<NpgsqlDataSource>();
    }
}
