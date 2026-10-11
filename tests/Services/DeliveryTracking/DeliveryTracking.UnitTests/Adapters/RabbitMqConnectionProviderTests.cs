using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Messaging.RabbitMqClient.Provider;

namespace DeliveryTracking.UnitTests.Adapters;

public class RabbitMqConnectionProviderTests
{
    [Fact]
    public void Constructor_DenganHostUnreachable_HarusGracefulDegradeTanpaCrash()
    {
        // arrange & act
        using var provider = new RabbitMqConnectionProvider(
            hostName: "127.0.0.1",
            userName: "invalid_user",
            password: "invalid_password",
            port: 59999, // port acak yang tidak membuka rabbitmq
            virtualHost: "/",
            logger: NullLogger<RabbitMqConnectionProvider>.Instance);

        // assert
        provider.IsConnected.Should().BeFalse();
        Action act = () => provider.CreateModel();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Constructor_DenganConfigurationUnreachable_HarusGracefulDegrade()
    {
        // arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMq:HostName"] = "127.0.0.1",
                ["RabbitMq:Port"] = "59998",
                ["RabbitMq:UserName"] = "guest",
                ["RabbitMq:Password"] = "guest"
            })
            .Build();

        // act
        using var provider = new RabbitMqConnectionProvider(config, NullLogger<RabbitMqConnectionProvider>.Instance);

        // assert
        provider.IsConnected.Should().BeFalse();
    }
}
