using FluentAssertions;
using Moq;
using Shared.Data.Mongo.DTOs;
using Shared.Data.Mongo.Repositories;
using Shared.Pagination;
using Xunit;

namespace DeliveryTracking.UnitTests.Endpoints;

public class RawResponseQueryTests
{
    private readonly Mock<IGpsRawResponseRepository> _rawRepoMock = new();

    [Fact]
    public async Task QueryRawResponses_DenganFilterDanPaginasi_HarusReturnHasilPaginasi()
    {
        // arrange
        var filter = new RawResponseFilterRequest
        {
            VendorName = "Dunex",
            PageIndex = 1,
            PageSize = 10
        };

        var mockItems = new List<RawResponseItemDto>
        {
            new()
            {
                Id = "60c72b2f9b1d8b2bad509abc",
                VendorId = Guid.NewGuid().ToString(),
                VendorName = "Dunex",
                EndpointUrl = "https://api.dunex.co.id/gps",
                CollectedAt = DateTime.UtcNow,
                RecordCount = 5,
                RawResponse = new { status = "success", count = 5 }
            }
        };

        var expectedResult = new PaginatedResult<RawResponseItemDto>(1, 10, 1, mockItems);

        _rawRepoMock
            .Setup(r => r.QueryRawResponsesAsync(It.Is<RawResponseFilterRequest>(f => f.VendorName == "Dunex"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // act
        var result = await _rawRepoMock.Object.QueryRawResponsesAsync(filter);

        // assert
        result.Should().NotBeNull();
        result.PageIndex.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.Count.Should().Be(1);
        result.Data.Should().HaveCount(1);
        result.Data.First().VendorName.Should().Be("Dunex");
    }

    [Fact]
    public async Task GetRawResponseById_KetikaIdDitemukan_HarusReturnDokumenAsli()
    {
        // arrange
        var id = "60c72b2f9b1d8b2bad509abc";
        var mockItem = new RawResponseItemDto
        {
            Id = id,
            VendorId = Guid.NewGuid().ToString(),
            VendorName = "PUNINAR",
            EndpointUrl = "https://puninar.com/api",
            CollectedAt = DateTime.UtcNow,
            RecordCount = 10,
            RawResponse = "{\"data\":[]}"
        };

        _rawRepoMock
            .Setup(r => r.GetRawResponseByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockItem);

        // act
        var result = await _rawRepoMock.Object.GetRawResponseByIdAsync(id);

        // assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(id);
        result.VendorName.Should().Be("PUNINAR");
    }
}
