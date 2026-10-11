using System.Data;
using DeliveryTracking.Infrastructure.Persistence;
using FluentAssertions;

namespace DeliveryTracking.UnitTests.Persistence;

public class DataReaderExtensionsTests
{
    [Fact]
    public void GetSafeStringOrDefault_KetikaKolomAda_HarusAmbilNilai()
    {
        var table = new DataTable();
        table.Columns.Add("nama", typeof(string));
        table.Rows.Add("Vendor A");
        using var reader = table.CreateDataReader();
        reader.Read();

        reader.GetSafeStringOrDefault("nama", "default").Should().Be("Vendor A");
        reader.GetSafeStringOrDefault("tidak_ada", "default").Should().Be("default");
    }

    [Fact]
    public void HelperExtensions_BerbagaiTipeData_HarusKembalikanNilaiAtauDefault()
    {
        var table = new DataTable();
        table.Columns.Add("str_col", typeof(string));
        table.Columns.Add("int_col", typeof(int));
        table.Columns.Add("dec_col", typeof(decimal));
        table.Columns.Add("dt_col", typeof(DateTime));
        table.Columns.Add("guid_col", typeof(Guid));
        table.Columns.Add("bool_col", typeof(bool));

        var guid = Guid.NewGuid();
        var dt = new DateTime(2026, 10, 11, 10, 0, 0, DateTimeKind.Utc);
        table.Rows.Add("val", 42, 123.45m, dt, guid, true);
        using var reader = table.CreateDataReader();
        reader.Read();

        reader.GetSafeStringOrDefault("str_col").Should().Be("val");
        reader.GetNullableStringOrDefault("str_col").Should().Be("val");
        reader.GetNullableStringOrDefault("unknown", "fallback").Should().Be("fallback");

        reader.GetSafeInt32OrDefault("int_col").Should().Be(42);
        reader.GetSafeInt32OrDefault("unknown", 99).Should().Be(99);

        reader.GetNullableDecimalOrDefault("dec_col").Should().Be(123.45m);
        reader.GetNullableDecimalOrDefault("unknown", 50m).Should().Be(50m);

        reader.GetNullableDateTimeOrDefault("dt_col").Should().Be(dt);
        reader.GetSafeDateTimeOrDefault("dt_col", DateTime.MinValue).Should().Be(dt);
        reader.GetSafeDateTimeOrDefault("unknown", dt).Should().Be(dt);

        reader.GetSafeGuidOrDefault("guid_col", Guid.Empty).Should().Be(guid);
        reader.GetNullableGuidOrDefault("guid_col").Should().Be(guid);
        reader.GetNullableGuidOrDefault("unknown").Should().BeNull();

        reader.GetBooleanOrDefault("bool_col").Should().BeTrue();
        reader.GetBooleanOrDefault("unknown", true).Should().BeTrue();
    }

    [Fact]
    public void MapToDeliveryTrackingSession_HarusPetakanSemuaField()
    {
        var table = new DataTable();
        table.Columns.Add("id", typeof(Guid));
        table.Columns.Add("delivery_no", typeof(string));
        table.Columns.Add("lpcd", typeof(string));
        table.Columns.Add("vendor_id", typeof(Guid));
        table.Columns.Add("match_key", typeof(string));
        table.Columns.Add("match_value", typeof(string));
        table.Columns.Add("clean_match_value", typeof(string));
        table.Columns.Add("driver_ktp", typeof(string));
        table.Columns.Add("status", typeof(string));
        table.Columns.Add("started_at", typeof(DateTime));
        table.Columns.Add("arrived_at", typeof(DateTime));
        table.Columns.Add("destination_geofence_id", typeof(Guid));
        table.Columns.Add("completed_at", typeof(DateTime));
        table.Columns.Add("completed_by", typeof(string));
        table.Columns.Add("completion_source", typeof(string));
        table.Columns.Add("is_deleted", typeof(bool));

        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        table.Rows.Add(id, "DO-001", "LP01", Guid.NewGuid(), "PlatNo", "B 123", "B123", "3201", "STARTED", now, now, Guid.NewGuid(), now, "admin", "SYSTEM", false);

        using var reader = table.CreateDataReader();
        reader.Read();

        var session = reader.MapToDeliveryTrackingSession();
        session.Should().NotBeNull();
        session.DeliveryNo.Should().Be("DO-001");
        session.MatchKey.Should().Be("PlatNo");
        session.CleanMatchValue.Should().Be("B123");
        session.Status.Should().Be("STARTED");
    }

    [Fact]
    public void GetFirstHelpers_KetikaKolomPertamaTidakAda_HarusAmbilKolomKeduaAtauDefault()
    {
        var table = new DataTable();
        table.Columns.Add("alt_str", typeof(string));
        table.Columns.Add("alt_guid", typeof(Guid));
        table.Columns.Add("alt_dec", typeof(decimal));

        var guid = Guid.NewGuid();
        table.Rows.Add("AltText", guid, 99.9m);
        using var reader = table.CreateDataReader();
        reader.Read();

        reader.GetFirstString("missing1", "alt_str").Should().Be("AltText");
        reader.GetFirstString("missing1", "missing2", "fallback").Should().Be("fallback");

        reader.GetFirstNullableString("missing1", "alt_str").Should().Be("AltText");
        reader.GetFirstNullableString("missing1", "missing2").Should().BeNull();

        reader.GetFirstGuid("missing1", "alt_guid").Should().Be(guid);
        reader.GetFirstGuid("missing1", "missing2").Should().Be(Guid.Empty);

        reader.GetFirstNullableDecimal("missing1", "alt_dec").Should().Be(99.9m);
        reader.GetFirstNullableDecimal("missing1", "missing2").Should().BeNull();
    }
}

