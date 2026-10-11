using System.Text;
using System.Text.Json;
using DeliveryTracking.Application.DTOs;
using DeliveryTracking.Application.UseCases.CompleteDeliveryJob;
using DeliveryTracking.Application.UseCases.StartDeliveryJob;
using DeliveryTracking.Workers;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Messaging.RabbitMqClient.Provider;

namespace DeliveryTracking.UnitTests.Workers;

public class DeliveryEventConsumerWorkerTests
{
    private readonly Mock<IRabbitMqConnectionProvider> _connectionProviderMock = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock = new();
    private readonly Mock<IServiceScope> _scopeMock = new();
    private readonly Mock<IServiceProvider> _serviceProviderMock = new();
    private readonly Mock<ISender> _mediatorMock = new();
    private readonly Mock<IModel> _channelMock = new();

    public DeliveryEventConsumerWorkerTests()
    {
        _scopeFactoryMock.Setup(s => s.CreateScope()).Returns(_scopeMock.Object);
        _scopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(ISender))).Returns(_mediatorMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_KetikaCancellationRequested_HarusBerhentiDenganAman()
    {
        // arrange
        _connectionProviderMock.SetupGet(c => c.IsConnected).Returns(false);
        var worker = new DeliveryEventConsumerWorker(
            _connectionProviderMock.Object,
            _scopeFactoryMock.Object,
            NullLogger<DeliveryEventConsumerWorker>.Instance);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // cancel immediately

        // act & assert
        var act = async () =>
        {
            await worker.StartAsync(cts.Token);
            await worker.StopAsync(CancellationToken.None);
        };

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExecuteAsync_KetikaEventStartedDiterima_HarusKirimCommandDanAck()
    {
        // arrange
        _connectionProviderMock.SetupGet(c => c.IsConnected).Returns(true);
        _connectionProviderMock.Setup(c => c.CreateModel()).Returns(_channelMock.Object);

        AsyncEventingBasicConsumer? capturedConsumer = null;
        _channelMock.Setup(c => c.BasicConsume(
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<IBasicConsumer>()))
            .Callback<string, bool, string, bool, bool, IDictionary<string, object>, IBasicConsumer>((q, ack, tag, nl, ex, args, consumer) =>
            {
                capturedConsumer = consumer as AsyncEventingBasicConsumer;
            })
            .Returns("consumer-tag");

        var worker = new DeliveryEventConsumerWorker(
            _connectionProviderMock.Object,
            _scopeFactoryMock.Object,
            NullLogger<DeliveryEventConsumerWorker>.Instance);

        using var cts = new CancellationTokenSource();
        var startTask = worker.StartAsync(cts.Token);

        await Task.Delay(100);

        capturedConsumer.Should().NotBeNull();

        var payload = JsonSerializer.Serialize(new StartDeliveryJobRequestDto
        {
            DeliveryNo = "DO-TEST-EVENT-001",
            Lpcd = "B 9999 XYZ",
            Val = "B 9999 XYZ"
        });

        var eventArgs = new BasicDeliverEventArgs
        {
            DeliveryTag = 123,
            RoutingKey = "delivery.active.started",
            Body = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(payload))
        };

        _mediatorMock.Setup(m => m.Send(It.IsAny<StartDeliveryJobCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StartDeliveryJobResultDto(
                200,
                "OK",
                "DO-TEST-EVENT-001",
                "B 9999 XYZ",
                "VEND1",
                "PlatNo",
                "B 9999 XYZ",
                "B9999XYZ",
                Guid.NewGuid(),
                "Test Vendor",
                DateTime.UtcNow));

        // act
        await capturedConsumer!.HandleBasicDeliver(
            eventArgs.ConsumerTag,
            eventArgs.DeliveryTag,
            eventArgs.Redelivered,
            eventArgs.Exchange,
            eventArgs.RoutingKey,
            eventArgs.BasicProperties,
            eventArgs.Body);

        // assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<StartDeliveryJobCommand>(cmd => cmd.Request.DeliveryNo == "DO-TEST-EVENT-001"),
            It.IsAny<CancellationToken>()), Times.Once);

        _channelMock.Verify(c => c.BasicAck(123, false), Times.Once);

        cts.Cancel();
        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_KetikaEventCompletedDiterima_HarusKirimCommandDanAck()
    {
        // arrange
        _connectionProviderMock.SetupGet(c => c.IsConnected).Returns(true);
        _connectionProviderMock.Setup(c => c.CreateModel()).Returns(_channelMock.Object);

        AsyncEventingBasicConsumer? capturedConsumer = null;
        _channelMock.Setup(c => c.BasicConsume(
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<IBasicConsumer>()))
            .Callback<string, bool, string, bool, bool, IDictionary<string, object>, IBasicConsumer>((q, ack, tag, nl, ex, args, consumer) =>
            {
                capturedConsumer = consumer as AsyncEventingBasicConsumer;
            })
            .Returns("consumer-tag");

        var worker = new DeliveryEventConsumerWorker(
            _connectionProviderMock.Object,
            _scopeFactoryMock.Object,
            NullLogger<DeliveryEventConsumerWorker>.Instance);

        using var cts = new CancellationTokenSource();
        var startTask = worker.StartAsync(cts.Token);

        await Task.Delay(100);

        capturedConsumer.Should().NotBeNull();

        var payload = JsonSerializer.Serialize(new CompleteDeliveryJobRequestDto
        {
            DeliveryNo = "DO-TEST-EVENT-002",
            CompletedBy = "AdminFinisher"
        });

        var eventArgs = new BasicDeliverEventArgs
        {
            DeliveryTag = 456,
            RoutingKey = "delivery.active.completed",
            Body = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(payload))
        };

        _mediatorMock.Setup(m => m.Send(It.IsAny<CompleteDeliveryJobCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompleteDeliveryJobResultDto(
                200,
                "OK",
                "DO-TEST-EVENT-002",
                DateTime.UtcNow));

        // act
        await capturedConsumer!.HandleBasicDeliver(
            eventArgs.ConsumerTag,
            eventArgs.DeliveryTag,
            eventArgs.Redelivered,
            eventArgs.Exchange,
            eventArgs.RoutingKey,
            eventArgs.BasicProperties,
            eventArgs.Body);

        // assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<CompleteDeliveryJobCommand>(cmd => cmd.Request.DeliveryNo == "DO-TEST-EVENT-002"),
            It.IsAny<CancellationToken>()), Times.Once);

        _channelMock.Verify(c => c.BasicAck(456, false), Times.Once);

        cts.Cancel();
        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_KetikaPayloadCorrupt_HarusNackDenganRequeueFalse()
    {
        // arrange
        _connectionProviderMock.SetupGet(c => c.IsConnected).Returns(true);
        _connectionProviderMock.Setup(c => c.CreateModel()).Returns(_channelMock.Object);

        AsyncEventingBasicConsumer? capturedConsumer = null;
        _channelMock.Setup(c => c.BasicConsume(
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<IBasicConsumer>()))
            .Callback<string, bool, string, bool, bool, IDictionary<string, object>, IBasicConsumer>((q, ack, tag, nl, ex, args, consumer) =>
            {
                capturedConsumer = consumer as AsyncEventingBasicConsumer;
            })
            .Returns("consumer-tag");

        var worker = new DeliveryEventConsumerWorker(
            _connectionProviderMock.Object,
            _scopeFactoryMock.Object,
            NullLogger<DeliveryEventConsumerWorker>.Instance);

        using var cts = new CancellationTokenSource();
        var startTask = worker.StartAsync(cts.Token);

        await Task.Delay(100);

        var eventArgs = new BasicDeliverEventArgs
        {
            DeliveryTag = 789,
            RoutingKey = "delivery.active.started",
            Body = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("{ invalid json !@#"))
        };

        // act
        await capturedConsumer!.HandleBasicDeliver(
            eventArgs.ConsumerTag,
            eventArgs.DeliveryTag,
            eventArgs.Redelivered,
            eventArgs.Exchange,
            eventArgs.RoutingKey,
            eventArgs.BasicProperties,
            eventArgs.Body);

        // assert: BasicNack harus dipanggil dengan requeue = false (dead-letter)
        _channelMock.Verify(c => c.BasicNack(789, false, false), Times.Once);

        cts.Cancel();
        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_KetikaRoutingKeyTidakDikenal_HarusBasicAckDanAbaikan()
    {
        // arrange
        _connectionProviderMock.SetupGet(c => c.IsConnected).Returns(true);
        _connectionProviderMock.Setup(c => c.CreateModel()).Returns(_channelMock.Object);

        AsyncEventingBasicConsumer? capturedConsumer = null;
        _channelMock.Setup(c => c.BasicConsume(
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<IBasicConsumer>()))
            .Callback<string, bool, string, bool, bool, IDictionary<string, object>, IBasicConsumer>((_, _, _, _, _, _, consumer) =>
            {
                capturedConsumer = consumer as AsyncEventingBasicConsumer;
            });

        var worker = new DeliveryEventConsumerWorker(
            _connectionProviderMock.Object,
            _scopeFactoryMock.Object,
            NullLogger<DeliveryEventConsumerWorker>.Instance);

        using var cts = new CancellationTokenSource();
        var startTask = worker.StartAsync(cts.Token);
        await Task.Delay(100);

        var eventArgs = new BasicDeliverEventArgs
        {
            DeliveryTag = 999,
            RoutingKey = "delivery.active.unrecognized",
            Body = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("{}"))
        };

        // act
        await capturedConsumer!.HandleBasicDeliver(
            eventArgs.ConsumerTag,
            eventArgs.DeliveryTag,
            eventArgs.Redelivered,
            eventArgs.Exchange,
            eventArgs.RoutingKey,
            eventArgs.BasicProperties,
            eventArgs.Body);

        // assert: Harus di-ack agar tidak menumpuk di antrean
        _channelMock.Verify(c => c.BasicAck(999, false), Times.Once);

        cts.Cancel();
        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_KetikaPayloadStartedKosongDeliveryNo_HarusNackDenganRequeueFalse()
    {
        // arrange
        _connectionProviderMock.SetupGet(c => c.IsConnected).Returns(true);
        _connectionProviderMock.Setup(c => c.CreateModel()).Returns(_channelMock.Object);

        AsyncEventingBasicConsumer? capturedConsumer = null;
        _channelMock.Setup(c => c.BasicConsume(
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<IBasicConsumer>()))
            .Callback<string, bool, string, bool, bool, IDictionary<string, object>, IBasicConsumer>((_, _, _, _, _, _, consumer) =>
            {
                capturedConsumer = consumer as AsyncEventingBasicConsumer;
            });

        var worker = new DeliveryEventConsumerWorker(
            _connectionProviderMock.Object,
            _scopeFactoryMock.Object,
            NullLogger<DeliveryEventConsumerWorker>.Instance);

        using var cts = new CancellationTokenSource();
        var startTask = worker.StartAsync(cts.Token);
        await Task.Delay(100);

        var eventArgs = new BasicDeliverEventArgs
        {
            DeliveryTag = 101,
            RoutingKey = "delivery.active.started",
            Body = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("{\"DeliveryNo\":\"\"}"))
        };

        // act
        await capturedConsumer!.HandleBasicDeliver(
            eventArgs.ConsumerTag,
            eventArgs.DeliveryTag,
            eventArgs.Redelivered,
            eventArgs.Exchange,
            eventArgs.RoutingKey,
            eventArgs.BasicProperties,
            eventArgs.Body);

        // assert
        _channelMock.Verify(c => c.BasicNack(101, false, false), Times.Once);

        cts.Cancel();
        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_KetikaPayloadCompletedKosongDeliveryNo_HarusNackDenganRequeueFalse()
    {
        // arrange
        _connectionProviderMock.SetupGet(c => c.IsConnected).Returns(true);
        _connectionProviderMock.Setup(c => c.CreateModel()).Returns(_channelMock.Object);

        AsyncEventingBasicConsumer? capturedConsumer = null;
        _channelMock.Setup(c => c.BasicConsume(
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<IBasicConsumer>()))
            .Callback<string, bool, string, bool, bool, IDictionary<string, object>, IBasicConsumer>((_, _, _, _, _, _, consumer) =>
            {
                capturedConsumer = consumer as AsyncEventingBasicConsumer;
            });

        var worker = new DeliveryEventConsumerWorker(
            _connectionProviderMock.Object,
            _scopeFactoryMock.Object,
            NullLogger<DeliveryEventConsumerWorker>.Instance);

        using var cts = new CancellationTokenSource();
        var startTask = worker.StartAsync(cts.Token);
        await Task.Delay(100);

        var eventArgs = new BasicDeliverEventArgs
        {
            DeliveryTag = 102,
            RoutingKey = "delivery.active.completed",
            Body = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("{\"DeliveryNo\":\"\"}"))
        };

        // act
        await capturedConsumer!.HandleBasicDeliver(
            eventArgs.ConsumerTag,
            eventArgs.DeliveryTag,
            eventArgs.Redelivered,
            eventArgs.Exchange,
            eventArgs.RoutingKey,
            eventArgs.BasicProperties,
            eventArgs.Body);

        // assert
        _channelMock.Verify(c => c.BasicNack(102, false, false), Times.Once);

        cts.Cancel();
        await worker.StopAsync(CancellationToken.None);
    }
}

