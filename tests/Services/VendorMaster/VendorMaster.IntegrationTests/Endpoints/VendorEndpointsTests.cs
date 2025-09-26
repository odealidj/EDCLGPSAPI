using VendorMaster.IntegrationTests.Fixtures;

namespace VendorMaster.IntegrationTests.Endpoints;

[Collection("IntegrationTests")]
public class VendorEndpointsTests(VendorMasterApiFactory factory) : IClassFixture<VendorMasterApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<HttpResponseMessage> PostVendorAsync(VendorDto dto, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/vendors")
        {
            Content = JsonContent.Create(dto)
        };
        request.Headers.Add("X-Idempotency-Key", idempotencyKey ?? Guid.NewGuid().ToString());
        return await _client.SendAsync(request);
    }

    [Fact]
    public async Task Post_DaftarVendorBaru_HarusReturn201CreatedDanTersimpanDiDatabase()
    {
        // arrange
        var vendorCode = $"VND_{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var request = new VendorDto
        {
            VendorCode = vendorCode,
            VendorName = "PT Telematika Integrasi",
            ProtocolType = ProtocolType.REST_API.ToString(),
            ProcessingStrategy = ProcessingStrategy.Individual.ToString(),
            Endpoints =
            [
                new VendorEndpointDto
                {
                    BaseUrl = "https://api.telematika.co.id/lastpos",
                    EndpointType = EndpointType.TELEMETRY_LAST_POS.ToString()
                }
            ],
            Lpcds =
            [
                new VendorLpcdDto { Lpcd = "B1001XYZ" }
            ],
            Mappings =
            [
                new VendorMappingDto { ResponseField = "latitude", MappedField = "Latitude", DataType = MappingDataType.NUMBER.ToString() }
            ]
        };

        // act
        var response = await PostVendorAsync(request);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<CreateVendorResult>();
        created.Should().NotBeNull();
        created!.Id.Should().NotBeEmpty();

        // verifikasi langsung ke database postgres
        using var db = factory.CreateDbContext();
        var saved = await db.GpsVendors
            .Include(x => x.GpsVendorEndpoints)
            .Include(x => x.Mappings)
            .Include(x => x.Lpcds)
            .FirstOrDefaultAsync(x => x.Id == created.Id);

        saved.Should().NotBeNull();
        saved!.VendorCode.Should().Be(vendorCode);
        saved.GpsVendorEndpoints.Should().HaveCount(1);
        saved.Mappings.Should().HaveCount(1);
        saved.Lpcds.Should().HaveCount(1);
    }

    [Fact]
    public async Task Get_AmbilDaftarVendor_HarusReturn200OKDanListPaginated()
    {
        // arrange
        var vendorCode = $"VND_{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var vendorName = $"Vendor {vendorCode}";
        var createRequest = new VendorDto
        {
            VendorCode = vendorCode,
            VendorName = vendorName,
            ProtocolType = ProtocolType.REST_API.ToString(),
            ProcessingStrategy = ProcessingStrategy.Individual.ToString()
        };
        await PostVendorAsync(createRequest);

        // act
        var response = await _client.GetAsync($"/api/v1/vendors?VendorName={vendorCode}&PageIndex=0&PageSize=10");

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<VendorDto>>();
        result.Should().NotBeNull();
        result!.PageIndex.Should().Be(1);
        result.Data.Should().Contain(x => x.VendorCode == vendorCode);
    }

    [Fact]
    public async Task Get_VendorsDefaultPagination_HarusReturnPageIndex1DanNextPrevMetadata()
    {
        // act
        var response = await _client.GetAsync("/api/v1/vendors");

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<VendorDto>>();
        result.Should().NotBeNull();
        result!.PageIndex.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.Prev.Should().BeFalse();
    }

    [Fact]
    public async Task Put_UpdateVendor_HarusReturn200OKDanFieldTerupdate()
    {
        // arrange - create vendor dulu
        var vendorCode = $"VND_{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var createResponse = await PostVendorAsync(new VendorDto
        {
            VendorCode = vendorCode,
            VendorName = "Vendor Awal",
            ProtocolType = ProtocolType.REST_API.ToString(),
            ProcessingStrategy = ProcessingStrategy.Individual.ToString()
        });
        var created = await createResponse.Content.ReadFromJsonAsync<CreateVendorResult>();

        // act - update nama vendor
        var updateRequest = new VendorDto
        {
            Id = created!.Id,
            VendorCode = vendorCode,
            VendorName = "Vendor Sudah Diupdate",
            ProtocolType = ProtocolType.MQTT.ToString(),
            ProcessingStrategy = ProcessingStrategy.Combined.ToString()
        };
        var updateResponse = await _client.PutAsJsonAsync("/api/v1/vendors", updateRequest);

        // assert
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = factory.CreateDbContext();
        var updated = await db.GpsVendors.FirstOrDefaultAsync(x => x.Id == created.Id);
        updated.Should().NotBeNull();
        updated!.VendorName.Should().Be("Vendor Sudah Diupdate");
        updated.ProtocolType.Should().Be(ProtocolType.MQTT.ToString());
    }

    [Fact]
    public async Task Delete_HapusVendor_HarusReturn200OKDanSoftDelete()
    {
        // arrange - create vendor
        var vendorCode = $"VND_{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var createResponse = await PostVendorAsync(new VendorDto
        {
            VendorCode = vendorCode,
            VendorName = "Vendor Mau Dihapus",
            ProtocolType = ProtocolType.REST_API.ToString(),
            ProcessingStrategy = ProcessingStrategy.Individual.ToString()
        });
        var created = await createResponse.Content.ReadFromJsonAsync<CreateVendorResult>();

        // act
        var deleteResponse = await _client.DeleteAsync($"/api/v1/vendors/{created!.Id}");

        // assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // pastikan di query ef normal tidak muncul (karena soft delete filter)
        using var db = factory.CreateDbContext();
        var vendor = await db.GpsVendors.FirstOrDefaultAsync(x => x.Id == created.Id);
        vendor.Should().BeNull();

        // pastikan di database aslinya IsDeleted = true
        var rawVendor = await db.GpsVendors.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == created.Id);
        rawVendor.Should().NotBeNull();
        rawVendor!.IsDeleted.Should().BeTrue();
    }
}
