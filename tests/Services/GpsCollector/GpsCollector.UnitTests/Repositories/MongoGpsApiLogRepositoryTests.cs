using FluentAssertions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Moq;
using Shared.Data.Mongo;
using Shared.Data.Mongo.Documents;
using Shared.Data.Mongo.Repositories;

namespace GpsCollector.UnitTests.Repositories;

public class MongoGpsApiLogRepositoryTests
{
    [Fact]
    public async Task InsertAsync_DenganLogBerhasil_HarusPanggilInsertOneAsync()
    {
        // arrange
        var mockCollection = new Mock<IMongoCollection<GpsApiLogDocument>>();
        var mockDb = new Mock<IMongoDatabase>();
        var settings = new MongoDbSettings { ApiLogsCollection = "gps_api_logs", ApiLogTtlDays = 7 };

        mockDb.Setup(d => d.GetCollection<GpsApiLogDocument>(It.IsAny<string>(), It.IsAny<MongoCollectionSettings>()))
            .Returns(mockCollection.Object);

        var repo = new MongoGpsApiLogRepository(mockDb.Object, Options.Create(settings));

        // act
        await repo.InsertAsync(
            Guid.NewGuid(), "Vendor A", "http://vendor.com", "PollPositions", 150, 200, true, null, null, 10, CancellationToken.None);

        // assert
        mockCollection.Verify(c => c.InsertOneAsync(
            It.Is<GpsApiLogDocument>(d => d.VendorName == "Vendor A" && d.Success == true && d.RecordCount == 10),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InsertAsync_DenganErrorDetailJson_HarusParseDanSimpan()
    {
        // arrange
        var mockCollection = new Mock<IMongoCollection<GpsApiLogDocument>>();
        var mockDb = new Mock<IMongoDatabase>();
        var settings = new MongoDbSettings { ApiLogsCollection = "gps_api_logs" };

        mockDb.Setup(d => d.GetCollection<GpsApiLogDocument>(It.IsAny<string>(), It.IsAny<MongoCollectionSettings>()))
            .Returns(mockCollection.Object);

        var repo = new MongoGpsApiLogRepository(mockDb.Object, Options.Create(settings));

        // act
        await repo.InsertAsync(
            Guid.NewGuid(), "Vendor Fail", "http://vendor.com", "PollPositions", 500, 500, false, "Internal Server Error", "{\"code\": 5001}", 0, CancellationToken.None);

        // assert
        mockCollection.Verify(c => c.InsertOneAsync(
            It.Is<GpsApiLogDocument>(d => d.Success == false && d.ErrorMessage == "Internal Server Error"),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
