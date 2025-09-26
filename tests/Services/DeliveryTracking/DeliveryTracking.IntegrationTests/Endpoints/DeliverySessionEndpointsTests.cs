namespace DeliveryTracking.IntegrationTests.Endpoints;

using DeliveryTracking.Application.DTOs;
using DeliveryTracking.IntegrationTests.Fixtures;
using FluentAssertions;
using NpgsqlTypes;
using System.Net;
using System.Net.Http.Json;
using Xunit;

[Collection("DeliveryTrackingIntegrationTests")]
public class DeliverySessionEndpointsTests(DeliveryTrackingApiFactory factory) : IClassFixture<DeliveryTrackingApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Post_StartJob_DenganPayloadValid_HarusReturn200OKDanTersimpanDiDatabase()
    {
        // arrange
        var deliveryNo = $"DO-START-{Guid.NewGuid():N}"[..15].ToUpperInvariant();
        var request = new StartDeliveryJobRequestDto
        {
            DeliveryNo = deliveryNo,
            Lpcd = "AJL", // Vendor AJL sudah ada di seed data
            Key = "PlatNo",
            Val = "B 9001 FXR",
            DriverKtp = "3201012345670001"
        };

        // act - panggil endpoint utama /api/v1/deliveries/start-job
        var response = await _client.PostAsJsonAsync("/api/v1/deliveries/start-job", request);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<StartDeliveryJobResultDto>();
        result.Should().NotBeNull();
        result!.Status.Should().Be(200);
        result.DeliveryNo.Should().Be(deliveryNo);
        result.Lpcd.Should().Be("AJL");
        result.Key.Should().Be("PlatNo");
        result.CleanValue.Should().Be("B9001FXR");
        result.VendorId.Should().NotBeNull();
        result.VendorCode.Should().Be("AJL");

        // verifikasi data langsung di tabel edcl.t_delivery_tracking_session
        var ds = factory.GetDataSource();
        await using var cmd = ds.CreateCommand("SELECT status, clean_match_value, lpcd FROM edcl.t_delivery_tracking_session WHERE delivery_no = $1;");
        cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, deliveryNo);
        await using var reader = await cmd.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        reader.GetString(0).Should().Be("STARTED");
        reader.GetString(1).Should().Be("B9001FXR");
        reader.GetString(2).Should().Be("AJL");
    }

    [Fact]
    public async Task Post_StartJob_AliasEndpoint_HarusReturn200OK()
    {
        // arrange
        var deliveryNo = $"DO-ALIAS-{Guid.NewGuid():N}"[..15].ToUpperInvariant();
        var request = new StartDeliveryJobRequestDto
        {
            DeliveryNo = deliveryNo,
            Lpcd = "AJL",
            Key = "PlatNo",
            Val = "B 1234 XYZ"
        };

        // act - panggil endpoint alias /api/v1/delivery-session/start
        var response = await _client.PostAsJsonAsync("/api/v1/delivery-session/start", request);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<StartDeliveryJobResultDto>();
        result.Should().NotBeNull();
        result!.DeliveryNo.Should().Be(deliveryNo);
        result.CleanValue.Should().Be("B1234XYZ");
        result.VendorCode.Should().Be("AJL");
    }

    [Fact]
    public async Task Post_StartJob_DenganMatchKeyDanMatchValue_HarusReturn200OKDanResolveVendorCodeOtomatis()
    {
        // arrange - Driver/EDCLAPI hanya kirim deliveryNo, lpcd, match_key, match_value (TANPA VendorCode)
        var deliveryNo = $"DO-MATCH-{Guid.NewGuid():N}"[..15].ToUpperInvariant();
        var request = new StartDeliveryJobRequestDto
        {
            DeliveryNo = deliveryNo,
            Lpcd = "AJL",
            MatchKey = "PlatNo",
            MatchValue = "B 5678 GHI"
        };

        // act
        var response = await _client.PostAsJsonAsync("/api/v1/deliveries/start-job", request);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<StartDeliveryJobResultDto>();
        result.Should().NotBeNull();
        result!.DeliveryNo.Should().Be(deliveryNo);
        result.Lpcd.Should().Be("AJL");
        result.VendorCode.Should().Be("AJL");
        result.Key.Should().Be("PlatNo");
        result.CleanValue.Should().Be("B5678GHI");
    }

    [Fact]
    public async Task Post_StartJob_DenganKeyTidakValid_HarusReturn400BadRequest()
    {
        // arrange
        var request = new StartDeliveryJobRequestDto
        {
            DeliveryNo = "DO-INVALID-KEY",
            Lpcd = "AJL",
            Key = "UnknownKey123",
            Val = "B 1234 XYZ"
        };

        // act
        var response = await _client.PostAsJsonAsync("/api/v1/deliveries/start-job", request);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_StartJob_DenganLpcdTidakDikenal_HarusReturn400BadRequest()
    {
        // arrange
        var request = new StartDeliveryJobRequestDto
        {
            DeliveryNo = "DO-UNKNOWN-LPCD",
            Lpcd = "LPCD_TIDAK_ADA_DI_DATABASE",
            Key = "PlatNo",
            Val = "B 1234 XYZ"
        };

        // act
        var response = await _client.PostAsJsonAsync("/api/v1/deliveries/start-job", request);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_DeliverySession_HarusReturn200OKDanDataSesiValid()
    {
        // arrange - buat sesi dulu
        var deliveryNo = $"DO-GET-{Guid.NewGuid():N}"[..15].ToUpperInvariant();
        var startRequest = new StartDeliveryJobRequestDto
        {
            DeliveryNo = deliveryNo,
            Lpcd = "AJL",
            Key = "PlatNo",
            Val = "B 8888 TMM"
        };
        await _client.PostAsJsonAsync("/api/v1/deliveries/start-job", startRequest);

        // act
        var response = await _client.GetAsync($"/api/v1/deliveries/session/{deliveryNo}");

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await response.Content.ReadFromJsonAsync<DeliveryTrackingSessionDto>();
        session.Should().NotBeNull();
        session!.DeliveryNo.Should().Be(deliveryNo);
        session.Lpcd.Should().Be("AJL");
        session.Status.Should().Be("STARTED");
        session.CleanValue.Should().Be("B8888TMM");
    }

    [Fact]
    public async Task Post_CompleteJob_HarusReturn200OKDanUpdateStatusCompleted()
    {
        // arrange - buat sesi dulu
        var deliveryNo = $"DO-CMPL-{Guid.NewGuid():N}"[..15].ToUpperInvariant();
        var startRequest = new StartDeliveryJobRequestDto
        {
            DeliveryNo = deliveryNo,
            Lpcd = "AJL",
            Key = "PlatNo",
            Val = "B 7777 TMM"
        };
        await _client.PostAsJsonAsync("/api/v1/deliveries/start-job", startRequest);

        // act - selesaikan job
        var completeRequest = new CompleteDeliveryJobRequestDto
        {
            DeliveryNo = deliveryNo,
            CompletedBy = "driver_supir_01",
            Source = "MANUAL_MOBILE"
        };
        var response = await _client.PostAsJsonAsync("/api/v1/deliveries/complete-job", completeRequest);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<CompleteDeliveryJobResultDto>();
        result.Should().NotBeNull();
        result!.Status.Should().Be(200);
        result.DeliveryNo.Should().Be(deliveryNo);

        // verifikasi di db status sudah COMPLETED
        var ds = factory.GetDataSource();
        await using var cmd = ds.CreateCommand("SELECT status FROM edcl.t_delivery_tracking_session WHERE delivery_no = $1;");
        cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, deliveryNo);
        var status = (string?)(await cmd.ExecuteScalarAsync());
        status.Should().Be("COMPLETED");
    }

    [Fact]
    public async Task Get_ActiveSessions_HarusReturn200OK()
    {
        // act
        var response = await _client.GetAsync("/api/v1/deliveries/sessions/active?limit=10");

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var sessions = await response.Content.ReadFromJsonAsync<IReadOnlyList<DeliveryTrackingSessionDto>>();
        sessions.Should().NotBeNull();
    }
}
