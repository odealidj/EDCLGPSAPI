namespace DeliveryTracking.IntegrationTests.Endpoints;

using DeliveryTracking.IntegrationTests.Fixtures;

[Collection("DeliveryTrackingIntegrationTests")]
public class DeliveryProgressEndpointsTests(DeliveryTrackingApiFactory factory) : IClassFixture<DeliveryTrackingApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<HttpResponseMessage> PostProgressAsync(DeliveryProgressDto request, string? idempotencyKey = null)
    {
        var msg = new HttpRequestMessage(HttpMethod.Post, "/api/v1/deliveries/progress")
        {
            Content = JsonContent.Create(request)
        };
        msg.Headers.Add("X-Idempotency-Key", idempotencyKey ?? Guid.NewGuid().ToString());
        return await _client.SendAsync(msg);
    }

    [Fact]
    public async Task Post_UpsertDeliveryProgressBaru_HarusReturn200DanIsCreatedTrue()
    {
        // arrange
        var deliveryNo = $"DO-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var request = new DeliveryProgressDto
        {
            DeliveryNo = deliveryNo,
            PlatNo = "B 1234 TMM",
            NoKtp = "3201123456780001",
            VendorName = "PT Trans Logistik Jaya",
            Lpcd = "LPCD-TMMIN-01"
        };

        // act
        var response = await PostProgressAsync(request);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SaveDeliveryProgressResult>();
        result.Should().NotBeNull();
        result!.DeliveryNo.Should().Be(deliveryNo);
        result.IsCreated.Should().BeTrue();
        result.Id.Should().NotBeEmpty();

        // verifikasi langsung via query ado.net ke postgres container
        var ds = factory.GetDataSource();
        await using var cmd = ds.CreateCommand("SELECT plat_no, vendor_name FROM edcl.tb_r_delivery_progress WHERE delivery_no = $1;");
        cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, deliveryNo);
        await using var reader = await cmd.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        reader.GetString(0).Should().Be("B 1234 TMM");
        reader.GetString(1).Should().Be("PT Trans Logistik Jaya");
    }

    [Fact]
    public async Task Post_UpsertDeliveryProgressSama_HarusUpdatePlatNoDanIsCreatedFalse()
    {
        // arrange - insert dulu data awal
        var deliveryNo = $"DO-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var initialRequest = new DeliveryProgressDto
        {
            DeliveryNo = deliveryNo,
            PlatNo = "B 1111 AAA",
            NoKtp = "3201111111110001",
            VendorName = "Vendor Awal",
            Lpcd = "LPCD-001"
        };
        await PostProgressAsync(initialRequest);

        // act - upsert dengan nomor DO yang sama tapi ganti plat nomor
        var updateRequest = new DeliveryProgressDto
        {
            DeliveryNo = deliveryNo,
            PlatNo = "B 9999 ZZZ",
            NoKtp = "3201111111110001",
            VendorName = "Vendor Armada Baru",
            Lpcd = "LPCD-002"
        };
        var response = await PostProgressAsync(updateRequest);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SaveDeliveryProgressResult>();
        result.Should().NotBeNull();
        result!.DeliveryNo.Should().Be(deliveryNo);
        result.IsCreated.Should().BeFalse(); // native postgresql ON CONFLICT DO UPDATE

        // verifikasi plat_no terupdate di database
        var ds = factory.GetDataSource();
        await using var cmd = ds.CreateCommand("SELECT plat_no, vendor_name FROM edcl.tb_r_delivery_progress WHERE delivery_no = $1;");
        cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, deliveryNo);
        await using var reader = await cmd.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        reader.GetString(0).Should().Be("B 9999 ZZZ");
        reader.GetString(1).Should().Be("Vendor Armada Baru");
    }

    [Fact]
    public async Task Get_ByDeliveryNo_HarusReturn200DanDetailProgress()
    {
        // arrange
        var deliveryNo = $"DO-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var request = new DeliveryProgressDto
        {
            DeliveryNo = deliveryNo,
            PlatNo = "D 4567 XYZ",
            NoKtp = "3273123456780002",
            VendorName = "PT Ekspedisi Mandiri",
            Lpcd = "LPCD-099"
        };
        await PostProgressAsync(request);

        // act
        var response = await _client.GetAsync($"/api/v1/deliveries/progress/{deliveryNo}");

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<DeliveryProgressDto>();
        detail.Should().NotBeNull();
        detail!.DeliveryNo.Should().Be(deliveryNo);
        detail.PlatNo.Should().Be("D 4567 XYZ");
        detail.VendorName.Should().Be("PT Ekspedisi Mandiri");
    }

    [Fact]
    public async Task Get_ByDeliveryNoTidakAda_HarusReturn404NotFound()
    {
        // act
        var response = await _client.GetAsync("/api/v1/deliveries/progress/DO-TIDAK-ADA-999");

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_DeliveryProgress_HarusReturn200DanTerhapusDariDb()
    {
        // arrange
        var deliveryNo = $"DO-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var request = new DeliveryProgressDto
        {
            DeliveryNo = deliveryNo,
            PlatNo = "B 5555 DEL",
            NoKtp = "3201555555550005",
            VendorName = "Vendor Mau Dihapus",
            Lpcd = "LPCD-DEL"
        };
        await PostProgressAsync(request);

        // act
        var deleteResponse = await _client.DeleteAsync($"/api/v1/deliveries/progress/{deliveryNo}");

        // assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // verifikasi row sudah tidak ada di database
        var ds = factory.GetDataSource();
        await using var cmd = ds.CreateCommand("SELECT COUNT(1) FROM edcl.tb_r_delivery_progress WHERE delivery_no = $1;");
        cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, deliveryNo);
        var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
        count.Should().Be(0);
    }

    [Fact]
    public async Task Post_ValidasiGagalSaatFieldWajibKosong_HarusReturn400BadRequest()
    {
        // arrange - field delivery no dan plat no kosong
        var invalidRequest = new DeliveryProgressDto
        {
            DeliveryNo = "",
            PlatNo = "",
            NoKtp = "",
            VendorName = null,
            Lpcd = null
        };

        // act
        var response = await PostProgressAsync(invalidRequest);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
