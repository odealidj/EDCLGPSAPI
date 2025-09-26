namespace DeliveryTracking.IntegrationTests.Endpoints;

using DeliveryTracking.IntegrationTests.Fixtures;

[Collection("DeliveryTrackingIntegrationTests")]
public class TrackingEndpointsTests(DeliveryTrackingApiFactory factory) : IClassFixture<DeliveryTrackingApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Get_TrackDelivery_HarusReturn200DanListTitikGpsDariStoredProcedure()
    {
        // arrange - siapkan data titik koordinat gps via bulk endpoint
        var deliveryNo = $"DO-ROUTE-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        var vendorId = Guid.NewGuid();
        var baseTime = DateTime.UtcNow.AddMinutes(-30);

        var gpsItems = Enumerable.Range(1, 10).Select(i => new BulkInsertGpsDeliveryItemDto(
            GpsVendorId: vendorId,
            GpsVendorName: "PT Trans Indo Tracker",
            DeliveryNo: deliveryNo,
            PlatNo: "B 8888 TRK",
            Lpcd: "LPCD-TMM-001",
            Datetime: baseTime.AddMinutes(i * 2),
            X: -6.210000m + (i * 0.001m),
            Y: 106.820000m + (i * 0.001m),
            Speed: 50.0m + i,
            Course: 90.0m,
            StreetName: $"Jl. Industri Sunter Blok {i}",
            DeviceId: "DEV-TK-01",
            NoKtp: "3201888888880001"
        )).ToList();

        var bulkResponse = await _client.PostAsJsonAsync("/api/v1/deliveries/telemetry/bulk", new BulkInsertGpsDeliveryRequestDto(gpsItems));
        bulkResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // act - panggil endpoint tracking via stored procedure sp_edclgps1_get_track_delivery
        var response = await _client.GetAsync($"/api/v1/deliveries/track?deliveryNo={deliveryNo}&density=1");

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var points = await response.Content.ReadFromJsonAsync<IReadOnlyList<TrackDeliveryPointDto>>();
        points.Should().NotBeNull();
        points.Should().HaveCount(10);

        var firstPoint = points![0];
        firstPoint.DeliveryNo.Should().Be(deliveryNo);
        firstPoint.PlatNo.Should().Be("B 8888 TRK");
        firstPoint.GpsVendor.Should().Be("PT Trans Indo Tracker");
        firstPoint.StreetName.Should().Be("Jl. Industri Sunter Blok 1");
    }

    [Fact]
    public async Task Get_TrackDeliveryYangBelumPernahAda_HarusReturn200DanListKosong()
    {
        // act
        var response = await _client.GetAsync("/api/v1/deliveries/track?deliveryNo=DO-KOSONG-12345");

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var points = await response.Content.ReadFromJsonAsync<IReadOnlyList<TrackDeliveryPointDto>>();
        points.Should().NotBeNull();
        points.Should().BeEmpty();
    }
}
