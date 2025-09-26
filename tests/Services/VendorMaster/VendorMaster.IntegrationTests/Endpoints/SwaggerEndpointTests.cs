using System.Text.Json.Nodes;
using VendorMaster.IntegrationTests.Fixtures;

namespace VendorMaster.IntegrationTests.Endpoints;

// pengujian ketersediaan swagger dan openapi documentation
[Collection("IntegrationTests")]
public class SwaggerEndpointTests(VendorMasterApiFactory factory) : IClassFixture<VendorMasterApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Get_SwaggerJson_HarusReturn200OKDanMendataSemuaEndpointVendorMaster()
    {
        // act
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        json.Should().NotBeNull();

        // verifikasi info api
        var info = json!["info"];
        info.Should().NotBeNull();
        info!["title"]!.GetValue<string>().Should().Contain("EDCL Vendor");

        // verifikasi paths endpoint terdaftar di openapi spec
        var paths = json["paths"]?.AsObject();
        paths.Should().NotBeNull();
        paths!.ContainsKey("/api/v1/vendors").Should().BeTrue();
        paths.ContainsKey("/api/v1/vendors/{id}").Should().BeTrue();
        paths.ContainsKey("/api/v1/geofence-destinations").Should().BeTrue();
    }

    [Fact]
    public async Task Get_SwaggerUI_HarusReturn200OKDenganHalamanHtml()
    {
        // act
        var response = await _client.GetAsync("/swagger/index.html");

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("swagger-ui");
    }
}
