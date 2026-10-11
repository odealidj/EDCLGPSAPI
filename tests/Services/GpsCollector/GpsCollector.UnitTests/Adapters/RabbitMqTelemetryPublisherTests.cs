using FluentAssertions;
using GpsCollector.Domain.Models;
using GpsCollector.Infrastructure.Adapters.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RabbitMQ.Client;
using Shared.Messaging.RabbitMqClient.Provider;

namespace GpsCollector.UnitTests.Adapters;

public class RabbitMqTelemetryPublisherTests
{
    [Fact]
    public async Task PublishTelemetryEventAsync_KetikaProviderNullAtauPointsKosong_HarusAbaikan()
    {
        // arrange
        var publisherNull = new RabbitMqTelemetryPublisher(NullLogger<RabbitMqTelemetryPublisher>.Instance, null);
        var mockProvider = new Mock<IRabbitMqConnectionProvider>();
        var publisherWithMock = new RabbitMqTelemetryPublisher(NullLogger<RabbitMqTelemetryPublisher>.Instance, mockProvider.Object);

        // act
        await publisherNull.PublishTelemetryEventAsync(Guid.NewGuid(), "Vendor A", Guid.NewGuid(), new List<TelemetryPoint>(), CancellationToken.None);
        await publisherWithMock.PublishTelemetryEventAsync(Guid.NewGuid(), "Vendor A", Guid.NewGuid(), new List<TelemetryPoint>(), CancellationToken.None);

        // assert
        mockProvider.Verify(p => p.GetChannel(), Times.Never);
    }

    [Fact]
    public async Task PublishTelemetryEventAsync_DenganPointsValid_HarusPublishKeRabbitMq()
    {
        // arrange
        var mockChannel = new Mock<IModel>();
        var mockProps = new Mock<IBasicProperties>();
        mockChannel.Setup(c => c.CreateBasicProperties()).Returns(mockProps.Object);
        mockChannel.SetupGet(c => c.IsClosed).Returns(false);

        var mockProvider = new Mock<IRabbitMqConnectionProvider>();
        mockProvider.Setup(p => p.GetChannel()).Returns(mockChannel.Object);

        var publisher = new RabbitMqTelemetryPublisher(NullLogger<RabbitMqTelemetryPublisher>.Instance, mockProvider.Object);

        var points = new List<TelemetryPoint>
        {
            new()
            {
                Id = Guid.NewGuid(),
                HeaderId = Guid.NewGuid(),
                Lpcd = "LPCD-1",
                PlatNo = "B 1234 ABC",
                DeviceId = "DEV-1",
                Datetime = DateTime.UtcNow,
                X = 106.8m,
                Y = -6.2m,
                Speed = 60m,
                Course = 90m,
                StreetName = "Jl. Tol Jagorawi"
            }
        };

        // act
        await publisher.PublishTelemetryEventAsync(Guid.NewGuid(), "Vendor Express", Guid.NewGuid(), points, CancellationToken.None);

        // assert
        mockChannel.Verify(c => c.BasicPublish(
            It.Is<string>(e => e == "edcl.gps.position"),
            It.Is<string>(rk => rk == "gps.truck.vendor express"),
            It.IsAny<bool>(),
            It.IsAny<IBasicProperties>(),
            It.IsAny<ReadOnlyMemory<byte>>()), Times.Once);
    }

    [Fact]
    public async Task PublishTelemetryEventAsync_KetikaChannelClosed_HarusAbaikanPublish()
    {
        // arrange
        var mockChannel = new Mock<IModel>();
        mockChannel.SetupGet(c => c.IsClosed).Returns(true);

        var mockProvider = new Mock<IRabbitMqConnectionProvider>();
        mockProvider.Setup(p => p.GetChannel()).Returns(mockChannel.Object);

        var publisher = new RabbitMqTelemetryPublisher(NullLogger<RabbitMqTelemetryPublisher>.Instance, mockProvider.Object);

        var points = new List<TelemetryPoint>
        {
            new()
            {
                Id = Guid.NewGuid(),
                HeaderId = Guid.NewGuid(),
                Lpcd = "LPCD-1",
                PlatNo = "B 1234 ABC",
                DeviceId = "DEV-1",
                Datetime = DateTime.UtcNow,
                X = 106.8m,
                Y = -6.2m,
                Speed = 60m,
                Course = 90m,
                StreetName = "Jl. Tol"
            }
        };

        // act
        await publisher.PublishTelemetryEventAsync(Guid.NewGuid(), "Vendor A", Guid.NewGuid(), points, CancellationToken.None);

        // assert
        mockChannel.Verify(c => c.BasicPublish(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<IBasicProperties>(),
            It.IsAny<ReadOnlyMemory<byte>>()), Times.Never);
    }

    [Fact]
    public async Task PublishTelemetryEventAsync_KetikaException_HarusTanganiGracefully()
    {
        // arrange
        var mockProvider = new Mock<IRabbitMqConnectionProvider>();
        mockProvider.Setup(p => p.GetChannel()).Throws(new InvalidOperationException("RabbitMQ connection dropped"));

        var publisher = new RabbitMqTelemetryPublisher(NullLogger<RabbitMqTelemetryPublisher>.Instance, mockProvider.Object);

        var points = new List<TelemetryPoint>
        {
            new()
            {
                Id = Guid.NewGuid(),
                HeaderId = Guid.NewGuid(),
                Lpcd = "LPCD-1",
                PlatNo = "B 1234 ABC",
                DeviceId = "DEV-1",
                Datetime = DateTime.UtcNow,
                X = 106.8m,
                Y = -6.2m,
                Speed = 60m,
                Course = 90m,
                StreetName = "Jl. Tol"
            }
        };

        // act
        var act = async () => await publisher.PublishTelemetryEventAsync(
            Guid.NewGuid(), "Vendor Crash", Guid.NewGuid(), points, CancellationToken.None);

        // assert
        await act.Should().NotThrowAsync();
    }
}
