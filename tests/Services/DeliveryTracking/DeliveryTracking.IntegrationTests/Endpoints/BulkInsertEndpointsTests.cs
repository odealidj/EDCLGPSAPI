namespace DeliveryTracking.IntegrationTests.Endpoints;

using DeliveryTracking.IntegrationTests.Fixtures;

[Collection("DeliveryTrackingIntegrationTests")]
public class BulkInsertEndpointsTests(DeliveryTrackingApiFactory factory) : IClassFixture<DeliveryTrackingApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Post_BulkProgressBinaryCopy_HarusInsert50RowsDenganCepatDanReturn200()
    {
        // arrange - buat 50 data progress unik
        var batchPrefix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var items = Enumerable.Range(1, 50).Select(i => new DeliveryProgressDto
        {
            DeliveryNo = $"DO-BULK-{batchPrefix}-{i:D3}",
            PlatNo = $"B {1000 + i} TMM",
            NoKtp = $"3201{i:D12}",
            VendorName = "PT Toyota Tsusho Logistik",
            Lpcd = $"LPCD-{i:D3}"
        }).ToList();

        var request = new BulkInsertDeliveryProgressRequestDto(items);

        // act
        var response = await _client.PostAsJsonAsync("/api/v1/deliveries/progress/bulk", request);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<BulkInsertResult>();
        result.Should().NotBeNull();
        result!.RowsInserted.Should().Be(50);
        result.EntityName.Should().Be("DeliveryProgress");

        // verifikasi langsung count di tabel postgres
        var ds = factory.GetDataSource();
        await using var cmd = ds.CreateCommand("SELECT COUNT(1) FROM edcl.tb_r_delivery_progress WHERE delivery_no LIKE $1;");
        cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, $"DO-BULK-{batchPrefix}-%");
        var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
        count.Should().Be(50);
    }

    [Fact]
    public async Task Post_BulkGpsTelemetriBinaryCopy_HarusInsert100PointsDenganCepatDanReturn200()
    {
        // arrange - generate 100 titik gps telemetri
        var deliveryNo = $"DO-TRACK-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        var vendorId = Guid.NewGuid();
        var baseTime = DateTime.UtcNow.AddHours(-1);

        var gpsItems = Enumerable.Range(1, 100).Select(i => new BulkInsertGpsDeliveryItemDto(
            GpsVendorId: vendorId,
            GpsVendorName: "PT Telematika Astra",
            DeliveryNo: deliveryNo,
            PlatNo: "B 7777 TOY",
            Lpcd: "LPCD-JKT-01",
            Datetime: baseTime.AddMinutes(i),
            X: -6.200000m + (i * 0.001m),
            Y: 106.816666m + (i * 0.001m),
            Speed: 45.5m,
            Course: 180.0m,
            StreetName: $"Jl. Raya Sunter KM {i}",
            DeviceId: "GPS-DEV-8899",
            NoKtp: "3201999999990001"
        )).ToList();

        var request = new BulkInsertGpsDeliveryRequestDto(gpsItems);

        // act
        var response = await _client.PostAsJsonAsync("/api/v1/deliveries/telemetry/bulk", request);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<BulkInsertResult>();
        result.Should().NotBeNull();
        result!.RowsInserted.Should().Be(100);
        result.EntityName.Should().Be("GpsDelivery");

        // verifikasi count di tabel edcl.tb_r_gps_delivery
        var ds = factory.GetDataSource();
        await using var cmd = ds.CreateCommand("SELECT COUNT(1) FROM edcl.tb_r_gps_delivery WHERE delivery_no = $1;");
        cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, deliveryNo);
        var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
        count.Should().Be(100);
    }

    [Fact]
    public async Task Post_BulkProgressItemKosong_HarusReturn400BadRequest()
    {
        // arrange
        var request = new BulkInsertDeliveryProgressRequestDto([]);

        // act
        var response = await _client.PostAsJsonAsync("/api/v1/deliveries/progress/bulk", request);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
