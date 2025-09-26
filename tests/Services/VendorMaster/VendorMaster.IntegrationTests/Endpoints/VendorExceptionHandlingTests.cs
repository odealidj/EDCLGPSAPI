using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using VendorMaster.IntegrationTests.Fixtures;

namespace VendorMaster.IntegrationTests.Endpoints;

[Collection("IntegrationTests")]
public class VendorExceptionHandlingTests(VendorMasterApiFactory factory) : IClassFixture<VendorMasterApiFactory>
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
    public async Task Post_VendorCodeDuplikat_HarusReturn409ConflictDanProblemDetails()
    {
        // arrange - daftarkan vendor pertama
        var vendorCode = $"VND_{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var request1 = new VendorDto
        {
            VendorCode = vendorCode,
            VendorName = "Vendor Pertama",
            ProtocolType = ProtocolType.REST_API.ToString(),
            ProcessingStrategy = ProcessingStrategy.Individual.ToString()
        };
        var firstResponse = await PostVendorAsync(request1);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // act - coba daftar lagi dengan vendor code yang persis sama tapi beda idempotency key
        var request2 = new VendorDto
        {
            VendorCode = vendorCode,
            VendorName = "Vendor Kedua Mencoba Duplikat",
            ProtocolType = ProtocolType.REST_API.ToString(),
            ProcessingStrategy = ProcessingStrategy.Individual.ToString()
        };
        var conflictResponse = await PostVendorAsync(request2);

        // assert
        conflictResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        conflictResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        // verifikasi header observabilitas
        conflictResponse.Headers.Contains("X-Trace-Id").Should().BeTrue();
        conflictResponse.Headers.Contains("X-Correlation-Id").Should().BeTrue();

        // verifikasi struktur rfc 7807 problem details
        var problem = await conflictResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.Conflict);
        problem.Title.Should().Be("Conflict");
        problem.Detail.Should().Contain(vendorCode);
        problem.Instance.Should().Be("/api/v1/vendors");
        problem.Extensions.Should().ContainKey("traceId");
        problem.Extensions.Should().ContainKey("correlationId");
    }

    [Fact]
    public async Task Post_PayloadDuplikatLpcd_HarusReturn400BadRequestDanProblemDetails()
    {
        // arrange
        var request = new VendorDto
        {
            VendorCode = $"VND_{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            VendorName = "Vendor LPCD Duplikat",
            ProtocolType = ProtocolType.REST_API.ToString(),
            ProcessingStrategy = ProcessingStrategy.Individual.ToString(),
            Lpcds =
            [
                new VendorLpcdDto { Lpcd = "B1234XYZ" },
                new VendorLpcdDto { Lpcd = "B1234XYZ" } // duplikat
            ]
        };

        // act
        var response = await PostVendorAsync(request);

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.BadRequest);
        problem.Detail.Should().Contain("Duplikat LPCD");
    }

    [Fact]
    public async Task Get_VendorIdTidakAda_HarusReturn404NotFoundDanProblemDetails()
    {
        // arrange
        var nonExistentId = Guid.NewGuid();

        // act
        var response = await _client.GetAsync($"/api/v1/vendors/{nonExistentId}");

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.NotFound);
        problem.Title.Should().Be("Resource Not Found");
    }
}
