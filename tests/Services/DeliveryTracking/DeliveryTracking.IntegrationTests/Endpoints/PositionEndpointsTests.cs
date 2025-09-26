using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeliveryTracking.Application.DTOs;
using DeliveryTracking.IntegrationTests.Fixtures;

namespace DeliveryTracking.IntegrationTests.Endpoints;

// tes integrasi end-to-end endpoint kueri posisi armada (dilebur dari positionquery.api)
public class PositionEndpointsTests(DeliveryTrackingApiFactory factory) : IClassFixture<DeliveryTrackingApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetPositionByPlate_ShouldReturn200Ok_WithValidPositionData()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/positions/B1234XYZ");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var position = await response.Content.ReadFromJsonAsync<LastPositionDto>();
        position.Should().NotBeNull();
        position!.PlatNo.Should().Be("B1234XYZ");
        position.VendorName.Should().Be("PT Vendor Tracker");
        position.DataSource.Should().Be("PostgreSQL");
    }

    [Fact]
    public async Task QueryPositions_WithPagination_ShouldReturn200Ok_WithPaginatedPositions()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/positions?pageIndex=1&pageSize=10");

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: content);
        var paginated = await response.Content.ReadFromJsonAsync<PaginatedPositionResponse>();
        paginated.Should().NotBeNull();
        paginated!.Count.Should().Be(1);
        paginated.Data.Should().NotBeEmpty();
        paginated.Data.First().PlatNo.Should().Be("B1234XYZ");
    }

    [Fact]
    public async Task LegacyGpsLastPosition_ShouldReturn200Ok_WithLastPositionEnvelope()
    {
        // Act (Endpoint backward compatibility legacy)
        var response = await _client.GetAsync("/gps-lastposition?platNo=B1234XYZ");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rawJson = await response.Content.ReadAsStringAsync();
        rawJson.Should().Contain("lastPosition");
        rawJson.Should().Contain("B1234XYZ");
    }
}
