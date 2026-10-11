using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using GpsCollector.Domain.Models;
using GpsCollector.Infrastructure.Adapters.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GpsCollector.UnitTests.Adapters;

public class VendorHttpFetcherTests
{
    private class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }

    [Fact]
    public async Task FetchEndpointAsync_TanpaAuth_HarusReturnSuksesDanKirimParams()
    {
        // arrange
        HttpRequestMessage? capturedRequest = null;
        var handler = new FakeHttpMessageHandler(req =>
        {
            capturedRequest = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"OK\",\"data\":[]}", Encoding.UTF8, "application/json")
            };
        });

        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handler));

        var fetcher = new VendorHttpFetcher(factoryMock.Object, NullLogger<VendorHttpFetcher>.Instance);

        var vendor = new VendorConfig
        {
            Id = Guid.NewGuid(),
            VendorName = "Vendor Tracking A",
            RequiredAuth = false,
            AuthType = "None"
        };

        var endpoint = new VendorEndpointConfig
        {
            Id = Guid.NewGuid(),
            GpsVendorId = vendor.Id,
            BaseUrl = "https://gps.vendor.id/api/positions",
            Method = "GET",
            ParamsJson = "{\"limit\":50,\"is_active\":true}",
            HeadersJson = "{\"X-Api-Client\":\"EDCL-GPS\"}"
        };

        // act
        var result = await fetcher.FetchEndpointAsync(vendor, endpoint, CancellationToken.None);

        // assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.ResponseBody.Should().Contain("\"status\":\"OK\"");
        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.Query.Should().Contain("limit=50");
        capturedRequest.Headers.Contains("X-Api-Client").Should().BeTrue();
    }

    [Fact]
    public async Task FetchEndpointAsync_DenganBasicAuth_HarusMenyematkanHeaderBasic()
    {
        // arrange
        HttpRequestMessage? capturedRequest = null;
        var handler = new FakeHttpMessageHandler(req =>
        {
            capturedRequest = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
            };
        });

        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handler));

        var fetcher = new VendorHttpFetcher(factoryMock.Object, NullLogger<VendorHttpFetcher>.Instance);

        var vendor = new VendorConfig
        {
            Id = Guid.NewGuid(),
            VendorName = "Vendor Basic Auth",
            RequiredAuth = true,
            AuthType = "Basic",
            Username = "edcl_user",
            Password = "secret_password"
        };

        var endpoint = new VendorEndpointConfig
        {
            Id = Guid.NewGuid(),
            GpsVendorId = vendor.Id,
            BaseUrl = "https://gps.vendor.id/api/v2/trucks",
            Method = "GET"
        };

        // act
        var result = await fetcher.FetchEndpointAsync(vendor, endpoint, CancellationToken.None);

        // assert
        result.IsSuccess.Should().BeTrue();
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Authorization.Should().NotBeNull();
        capturedRequest.Headers.Authorization!.Scheme.Should().Be("Basic");

        var decodedCreds = Encoding.UTF8.GetString(Convert.FromBase64String(capturedRequest.Headers.Authorization!.Parameter!));
        decodedCreds.Should().Be("edcl_user:secret_password");
    }

    [Fact]
    public async Task FetchEndpointAsync_DenganBearerAuth_HarusFetchDanCacheToken()
    {
        // arrange
        var authCallCount = 0;
        var vendorId = Guid.NewGuid();

        var handler = new FakeHttpMessageHandler(req =>
        {
            if (req.RequestUri!.ToString().Contains("/oauth/token"))
            {
                authCallCount++;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"token\":\"ey.jwt.mock.token.123\",\"expires_in\":3600}", Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"positions\":[{\"lat\":-6.2,\"lon\":106.8}]}", Encoding.UTF8, "application/json")
            };
        });

        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handler));

        var fetcher = new VendorHttpFetcher(factoryMock.Object, NullLogger<VendorHttpFetcher>.Instance);

        var authConfig = new VendorAuthConfig
        {
            Id = Guid.NewGuid(),
            GpsVendorId = vendorId,
            BaseUrl = "https://auth.vendor.id/oauth/token",
            Method = "POST",
            AuthType = "Basic",
            Username = "client_id",
            Password = "client_secret",
            ContentType = "application/json",
            BodiesJson = "{\"grant_type\":\"client_credentials\"}"
        };

        var vendor = new VendorConfig
        {
            Id = vendorId,
            VendorName = "Vendor Bearer",
            RequiredAuth = true,
            AuthType = "Bearer",
            Auth = authConfig
        };

        var endpoint = new VendorEndpointConfig
        {
            Id = Guid.NewGuid(),
            GpsVendorId = vendorId,
            BaseUrl = "https://api.vendor.id/tracking",
            Method = "GET"
        };

        // act 1 - pemanggilan pertama harus fetch token
        var result1 = await fetcher.FetchEndpointAsync(vendor, endpoint, CancellationToken.None);

        // act 2 - pemanggilan kedua harus menggunakan cache token tanpa call auth ulang
        var result2 = await fetcher.FetchEndpointAsync(vendor, endpoint, CancellationToken.None);

        // assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        authCallCount.Should().Be(1, "karena token kedua diambil dari ConcurrentDictionary cache");
    }

    [Fact]
    public async Task FetchEndpointAsync_DenganRequestBodyDanPostMethod_HarusKirimContent()
    {
        // arrange
        HttpRequestMessage? capturedRequest = null;
        var handler = new FakeHttpMessageHandler(req =>
        {
            capturedRequest = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"result\":\"accepted\"}", Encoding.UTF8, "application/json")
            };
        });

        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handler));

        var fetcher = new VendorHttpFetcher(factoryMock.Object, NullLogger<VendorHttpFetcher>.Instance);

        var vendor = new VendorConfig
        {
            Id = Guid.NewGuid(),
            VendorName = "Vendor POST",
            RequiredAuth = false,
            AuthType = "None"
        };

        var endpoint = new VendorEndpointConfig
        {
            Id = Guid.NewGuid(),
            GpsVendorId = vendor.Id,
            BaseUrl = "https://api.vendor.id/batch-locations",
            Method = "POST",
            BodiesJson = "{\"deviceIds\":[\"DEV-1\",\"DEV-2\"]}"
        };

        // act
        var result = await fetcher.FetchEndpointAsync(vendor, endpoint, CancellationToken.None);

        // assert
        result.IsSuccess.Should().BeTrue();
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Method.Should().Be("POST");
        var body = await capturedRequest.Content!.ReadAsStringAsync();
        body.Should().Contain("DEV-1");
    }

    [Fact]
    public async Task FetchEndpointAsync_KetikaHttpThrowException_HarusReturnIsSuccessFalseDan500()
    {
        // arrange
        var handler = new FakeHttpMessageHandler(_ =>
        {
            throw new HttpRequestException("Network failure to vendor host");
        });

        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handler));

        var fetcher = new VendorHttpFetcher(factoryMock.Object, NullLogger<VendorHttpFetcher>.Instance);

        var vendor = new VendorConfig
        {
            Id = Guid.NewGuid(),
            VendorName = "Vendor Down",
            RequiredAuth = false,
            AuthType = "None"
        };

        var endpoint = new VendorEndpointConfig
        {
            Id = Guid.NewGuid(),
            GpsVendorId = vendor.Id,
            BaseUrl = "https://offline.vendor.id/positions",
            Method = "GET"
        };

        // act
        var result = await fetcher.FetchEndpointAsync(vendor, endpoint, CancellationToken.None);

        // assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(500);
        result.ReasonPhrase.Should().Contain("Network failure");
    }
}
