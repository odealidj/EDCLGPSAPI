using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shared.Infrastructure.LeaderElection;
using StackExchange.Redis;

namespace GpsCollector.UnitTests.Infrastructure;

public class RedisLeaderElectionTests
{
    [Fact]
    public async Task TryAcquireAsync_KetikaRedisNull_HarusFallbackSebagaiLeader()
    {
        // arrange
        var election = new RedisLeaderElection(NullLogger<RedisLeaderElection>.Instance, null);

        // act
        var result = await election.TryAcquireAsync(TimeSpan.FromSeconds(30), CancellationToken.None);

        // assert
        result.Should().BeTrue();
        election.IsLeader.Should().BeTrue();
    }

    [Fact]
    public async Task TryAcquireAsync_KetikaRedisAdaDanLockBerhasil_HarusReturnTrue()
    {
        // arrange
        var redisMock = new Mock<IConnectionMultiplexer>();
        var dbMock = new Mock<IDatabase>();
        redisMock.SetupGet(r => r.IsConnected).Returns(true);
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);

        dbMock.SetReturnsDefault<Task<bool>>(Task.FromResult(true));

        var election = new RedisLeaderElection(NullLogger<RedisLeaderElection>.Instance, redisMock.Object);

        // act
        var result = await election.TryAcquireAsync(TimeSpan.FromSeconds(60), CancellationToken.None);

        // assert
        result.Should().BeTrue();
        election.IsLeader.Should().BeTrue();
    }

    [Fact]
    public async Task TryAcquireAsync_KetikaNodeLainSudahMenjadiLeader_HarusReturnFalse()
    {
        // arrange
        var redisMock = new Mock<IConnectionMultiplexer>();
        var dbMock = new Mock<IDatabase>();
        redisMock.SetupGet(r => r.IsConnected).Returns(true);
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);

        dbMock.SetReturnsDefault<Task<bool>>(Task.FromResult(false));
        dbMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync("OTHER_HOST_NODE_999");

        var election = new RedisLeaderElection(NullLogger<RedisLeaderElection>.Instance, redisMock.Object);

        // act
        var result = await election.TryAcquireAsync(TimeSpan.FromSeconds(60), CancellationToken.None);

        // assert
        result.Should().BeFalse();
        election.IsLeader.Should().BeFalse();
    }

    [Fact]
    public async Task ReleaseAsync_HarusHapusKeyJikaMilikSendiri()
    {
        // arrange
        var redisMock = new Mock<IConnectionMultiplexer>();
        var dbMock = new Mock<IDatabase>();
        redisMock.SetupGet(r => r.IsConnected).Returns(true);
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);
        var expectedNodeId = Environment.GetEnvironmentVariable("HOSTNAME") ?? Environment.MachineName;
        dbMock.SetReturnsDefault<Task<bool>>(Task.FromResult(true));
        dbMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)expectedNodeId);

        var election = new RedisLeaderElection(NullLogger<RedisLeaderElection>.Instance, redisMock.Object);
        await election.TryAcquireAsync(TimeSpan.FromSeconds(60), CancellationToken.None);

        // act
        await election.ReleaseAsync();

        // assert
        election.IsLeader.Should().BeFalse();
    }

    [Fact]
    public async Task RenewAsync_KetikaRedisNull_HarusReturnTrue()
    {
        var election = new RedisLeaderElection(NullLogger<RedisLeaderElection>.Instance, null);
        var result = await election.RenewAsync(TimeSpan.FromSeconds(30), CancellationToken.None);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RenewAsync_KetikaBukanLeader_HarusReturnFalse()
    {
        var redisMock = new Mock<IConnectionMultiplexer>();
        var dbMock = new Mock<IDatabase>();
        redisMock.SetupGet(r => r.IsConnected).Returns(true);
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);
        dbMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync("ANOTHER_LEADER");

        var election = new RedisLeaderElection(NullLogger<RedisLeaderElection>.Instance, redisMock.Object);
        var result = await election.RenewAsync(TimeSpan.FromSeconds(30), CancellationToken.None);

        result.Should().BeFalse();
        election.IsLeader.Should().BeFalse();
    }
}
