using System.Text.Json;
using DeliveryTracking.Application.DTOs;
using DeliveryTracking.Infrastructure.Adapters.Messaging;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RabbitMQ.Client;
using Shared.Messaging.RabbitMqClient.Provider;

namespace DeliveryTracking.UnitTests.Adapters;

public class RabbitMqDeliveryEventPublisherTests
{
    [Fact]
    public async Task PublishDeliveryDeliveredAsync_KetikaProviderNull_HarusAbaikan()
    {
        // arrange
        var publisher = new RabbitMqDeliveryEventPublisher(NullLogger<RabbitMqDeliveryEventPublisher>.Instance, null);
        var dto = new DeliveryDeliveredEventDto(
            DeliveryNo: "DO-001",
            Status: "DELIVERED",
            PlatNo: "B 1234 ABC",
            Lpcd: "LPCD-1",
            DestinationGeofenceId: Guid.NewGuid(),
            DestinationName: "GEOFENCE-01",
            DeliveredAt: DateTime.UtcNow,
            Latitude: -6.2m,
            Longitude: 106.8m
        );

        // act
        var act = async () => await publisher.PublishDeliveryDeliveredAsync(dto, CancellationToken.None);

        // assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishDeliveryDeliveredAsync_KetikaChannelAda_HarusPublishKeExchange()
    {
        // arrange
        var mockChannel = new Mock<IModel>();
        var mockProps = new Mock<IBasicProperties>();
        mockChannel.Setup(c => c.CreateBasicProperties()).Returns(mockProps.Object);

        var mockProvider = new Mock<IRabbitMqConnectionProvider>();
        mockProvider.Setup(p => p.CreateModel()).Returns(mockChannel.Object);

        var publisher = new RabbitMqDeliveryEventPublisher(NullLogger<RabbitMqDeliveryEventPublisher>.Instance, mockProvider.Object);
        var dto = new DeliveryDeliveredEventDto(
            DeliveryNo: "DO-999",
            Status: "DELIVERED",
            PlatNo: "B 9999 XYZ",
            Lpcd: "LPCD-TMM",
            DestinationGeofenceId: Guid.NewGuid(),
            DestinationName: "CUSTOMER-DEST",
            DeliveredAt: DateTime.UtcNow,
            Latitude: -6.21m,
            Longitude: 106.82m
        );

        // act
        await publisher.PublishDeliveryDeliveredAsync(dto, CancellationToken.None);

        // assert
        mockChannel.Verify(c => c.BasicPublish(
            It.Is<string>(e => e == "edcl.delivery"),
            It.Is<string>(rk => rk == "delivery.status.delivered"),
            It.IsAny<bool>(),
            It.IsAny<IBasicProperties>(),
            It.IsAny<ReadOnlyMemory<byte>>()), Times.Once);
    }

    [Fact]
    public async Task PublishDeliveryDeliveredAsync_KetikaException_HarusTanganiTanpaCrash()
    {
        // arrange
        var mockProvider = new Mock<IRabbitMqConnectionProvider>();
        mockProvider.Setup(p => p.CreateModel()).Throws(new InvalidOperationException("RabbitMQ connection down"));

        var publisher = new RabbitMqDeliveryEventPublisher(NullLogger<RabbitMqDeliveryEventPublisher>.Instance, mockProvider.Object);
        var dto = new DeliveryDeliveredEventDto(
            DeliveryNo: "DO-ERR",
            Status: "DELIVERED",
            PlatNo: "B 1234 ABC",
            Lpcd: "LPCD-1",
            DestinationGeofenceId: null,
            DestinationName: null,
            DeliveredAt: DateTime.UtcNow,
            Latitude: -6.2m,
            Longitude: 106.8m
        );

        // act
        var act = async () => await publisher.PublishDeliveryDeliveredAsync(dto, CancellationToken.None);

        // assert
        await act.Should().NotThrowAsync();
    }
}
