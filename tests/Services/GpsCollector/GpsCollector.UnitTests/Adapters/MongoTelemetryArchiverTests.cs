using FluentAssertions;
using GpsCollector.Infrastructure.Adapters.Archive;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shared.Data.Mongo.Repositories;

namespace GpsCollector.UnitTests.Adapters;

public class MongoTelemetryArchiverTests
{
    [Fact]
    public async Task ArchiveRawResponseAsync_KetikaRepoNullAtauJsonKosong_HarusAbaikan()
    {
        // arrange
        var archiverNullRepo = new MongoTelemetryArchiver(NullLogger<MongoTelemetryArchiver>.Instance, null);
        var mockRepo = new Mock<IGpsRawResponseRepository>();
        var archiverWithRepo = new MongoTelemetryArchiver(NullLogger<MongoTelemetryArchiver>.Instance, mockRepo.Object);

        // act
        await archiverNullRepo.ArchiveRawResponseAsync(Guid.NewGuid(), "Vendor A", "http://api.com", "{}", 1, CancellationToken.None);
        await archiverWithRepo.ArchiveRawResponseAsync(Guid.NewGuid(), "Vendor A", "http://api.com", "", 1, CancellationToken.None);
        await archiverWithRepo.ArchiveRawResponseAsync(Guid.NewGuid(), "Vendor A", "http://api.com", "   ", 1, CancellationToken.None);

        // assert
        mockRepo.Verify(r => r.InsertAsync(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ArchiveRawResponseAsync_PayloadPertama_HarusInsertKeMongo()
    {
        // arrange
        var mockRepo = new Mock<IGpsRawResponseRepository>();
        var archiver = new MongoTelemetryArchiver(NullLogger<MongoTelemetryArchiver>.Instance, mockRepo.Object);
        var vendorId = Guid.NewGuid();
        var url = "https://gps.vendor.id/api/positions";
        var rawJson = "{\"trucks\":[{\"id\":\"T1\",\"lat\":-6.2,\"lon\":106.8}]}";

        // act
        await archiver.ArchiveRawResponseAsync(vendorId, "Vendor Alpha", url, rawJson, 1, CancellationToken.None);

        // assert
        mockRepo.Verify(r => r.InsertAsync(
            vendorId,
            "Vendor Alpha",
            url,
            rawJson,
            1,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ArchiveRawResponseAsync_PayloadIdentik_HarusDilewatiOlehFilterRAM()
    {
        // arrange
        var mockRepo = new Mock<IGpsRawResponseRepository>();
        var archiver = new MongoTelemetryArchiver(NullLogger<MongoTelemetryArchiver>.Instance, mockRepo.Object);
        var vendorId = Guid.NewGuid();
        var url = "https://gps.vendor.id/api/positions/dedup-test";
        var rawJson = "{\"status\":\"ok\",\"timestamp\":12345,\"data\":[]}";

        // act 1 - panggilan pertama
        await archiver.ArchiveRawResponseAsync(vendorId, "Vendor Beta", url, rawJson, 0, CancellationToken.None);

        // act 2 - panggilan kedua dengan payload identik
        await archiver.ArchiveRawResponseAsync(vendorId, "Vendor Beta", url, rawJson, 0, CancellationToken.None);

        // assert: InsertAsync hanya boleh dipanggil 1 kali karena panggilan kedua di-filter di RAM
        mockRepo.Verify(r => r.InsertAsync(
            vendorId,
            "Vendor Beta",
            url,
            rawJson,
            0,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ArchiveRawResponseAsync_PayloadBerubah_HarusInsertUlangDanUpdateHash()
    {
        // arrange
        var mockRepo = new Mock<IGpsRawResponseRepository>();
        var archiver = new MongoTelemetryArchiver(NullLogger<MongoTelemetryArchiver>.Instance, mockRepo.Object);
        var vendorId = Guid.NewGuid();
        var url = "https://gps.vendor.id/api/positions/change-test";
        var rawJson1 = "{\"truck\":\"T1\",\"lat\":-6.2001}";
        var rawJson2 = "{\"truck\":\"T1\",\"lat\":-6.2050}"; // koordinat bergerak

        // act
        await archiver.ArchiveRawResponseAsync(vendorId, "Vendor Gamma", url, rawJson1, 1, CancellationToken.None);
        await archiver.ArchiveRawResponseAsync(vendorId, "Vendor Gamma", url, rawJson2, 1, CancellationToken.None);

        // assert: Kedua panggilan harus masuk karena payload berbeda
        mockRepo.Verify(r => r.InsertAsync(
            vendorId,
            "Vendor Gamma",
            url,
            It.IsAny<string>(),
            1,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ArchiveRawResponseAsync_KetikaMongoThrowException_HarusTanganiTanpaCrash()
    {
        // arrange
        var mockRepo = new Mock<IGpsRawResponseRepository>();
        mockRepo.Setup(r => r.InsertAsync(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Mongo connection timeout"));

        var archiver = new MongoTelemetryArchiver(NullLogger<MongoTelemetryArchiver>.Instance, mockRepo.Object);

        // act
        var act = async () => await archiver.ArchiveRawResponseAsync(
            Guid.NewGuid(), "Vendor Down", "http://api.com/err", "{\"error\":true}", 1, CancellationToken.None);

        // assert
        await act.Should().NotThrowAsync();
    }
}
