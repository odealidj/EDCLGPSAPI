using VendorMaster.IntegrationTests.Fixtures;

namespace VendorMaster.IntegrationTests.Endpoints;

[Collection("IntegrationTests")]
public class HealthCheckTests(VendorMasterApiFactory factory) : IClassFixture<VendorMasterApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Get_HealthCheck_HarusReturn200OKHealthy()
    {
        // act
        var response = await _client.GetAsync("/hc");

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Be("Healthy");
    }

    [Fact]
    public async Task Get_RootServiceInfo_HarusReturn200OKDenganMetadataService()
    {
        // act
        var response = await _client.GetAsync("/");

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonObject>();
        json.Should().NotBeNull();
        json!["service"]!.GetValue<string>().Should().Be("EDCL Vendor & Geofence Master Service");
        json["architecture"]!.GetValue<string>().Should().Be("Hexagonal");
        json["status"]!.GetValue<string>().Should().Be("Healthy");
    }
}
