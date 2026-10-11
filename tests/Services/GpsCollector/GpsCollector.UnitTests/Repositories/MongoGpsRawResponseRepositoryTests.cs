using FluentAssertions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Moq;
using Shared.Data.Mongo;
using Shared.Data.Mongo.Documents;
using Shared.Data.Mongo.Repositories;

namespace GpsCollector.UnitTests.Repositories;

public class MongoGpsRawResponseRepositoryTests
{
    [Fact]
    public async Task InsertAsync_DenganJsonValid_HarusPanggilInsertOneAsync()
    {
        // arrange
        var mockCollection = new Mock<IMongoCollection<GpsRawResponseDocument>>();
        var mockDb = new Mock<IMongoDatabase>();
        var settings = new MongoDbSettings
        {
            RawResponsesCollection = "gps_raw_responses",
            RawResponseTtlDays = 30
        };

        mockDb.Setup(d => d.GetCollection<GpsRawResponseDocument>(It.IsAny<string>(), It.IsAny<MongoCollectionSettings>()))
            .Returns(mockCollection.Object);

        var repo = new MongoGpsRawResponseRepository(mockDb.Object, Options.Create(settings));

        var vendorId = Guid.NewGuid();
        var rawJson = "{\"status\":200,\"data\":[{\"plat\":\"B 1234 ABC\"}]}";

        // act
        await repo.InsertAsync(vendorId, "Vendor A", "http://vendor.com", rawJson, 1, "batch-1", CancellationToken.None);

        // assert
        mockCollection.Verify(c => c.InsertOneAsync(
            It.Is<GpsRawResponseDocument>(d => d.VendorName == "Vendor A" && d.BatchId == "batch-1" && d.RecordCount == 1),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InsertAsync_DenganTextBukanJson_HarusTetapDisimpanSebagaiRawText()
    {
        // arrange
        var mockCollection = new Mock<IMongoCollection<GpsRawResponseDocument>>();
        var mockDb = new Mock<IMongoDatabase>();
        var settings = new MongoDbSettings { RawResponsesCollection = "gps_raw_responses" };

        mockDb.Setup(d => d.GetCollection<GpsRawResponseDocument>(It.IsAny<string>(), It.IsAny<MongoCollectionSettings>()))
            .Returns(mockCollection.Object);

        var repo = new MongoGpsRawResponseRepository(mockDb.Object, Options.Create(settings));

        // act
        await repo.InsertAsync(Guid.NewGuid(), "Vendor Malformed", "http://vendor.com", "Plain text not a json", 0, null, CancellationToken.None);

        // assert
        mockCollection.Verify(c => c.InsertOneAsync(
            It.Is<GpsRawResponseDocument>(d => d.VendorName == "Vendor Malformed" && d.RecordCount == 0),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
