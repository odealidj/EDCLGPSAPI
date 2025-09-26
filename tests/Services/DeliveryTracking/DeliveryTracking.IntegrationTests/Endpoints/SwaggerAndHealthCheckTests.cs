namespace DeliveryTracking.IntegrationTests.Endpoints;

using DeliveryTracking.IntegrationTests.Fixtures;

[Collection("DeliveryTrackingIntegrationTests")]
public class SwaggerAndHealthCheckTests(DeliveryTrackingApiFactory factory) : IClassFixture<DeliveryTrackingApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Get_RootInfo_HarusReturn200DanRuntimeNet10()
    {
        // act
        var response = await _client.GetAsync("/");

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(".NET 10 (ReadyToRun)");
        content.Should().Contain("ADO.NET Npgsql (Binary COPY Bulk)");
    }

    [Fact]
    public async Task Get_HealthCheck_HarusReturn200Healthy()
    {
        // act
        var response = await _client.GetAsync("/hc");

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("Healthy");
    }

    [Fact]
    public async Task Get_SwaggerDocJson_HarusReturn200DanMemuatEndpoints()
    {
        // act
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("/api/v1/deliveries/progress");
        json.Should().Contain("/api/v1/deliveries/progress/bulk");
        json.Should().Contain("/api/v1/deliveries/telemetry/bulk");
        json.Should().Contain("/api/v1/deliveries/track");
    }
}
