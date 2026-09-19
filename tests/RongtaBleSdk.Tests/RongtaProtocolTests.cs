using FluentAssertions;
using Xunit;

namespace RongtaBleSdk.Tests;

public class RongtaProtocolTests
{
    [Fact]
    public void KnownUuidPairs_ShouldContainValidatedRpp30Uuids()
    {
        // Act
        var pairs = RongtaProtocol.KnownUuidPairs;

        // Assert
        pairs.Should().NotBeEmpty();

        var primary = pairs[0];
        primary.ServiceUuid.Should().Be("49535343-fe7d-4ae5-8fa9-9fafd205e455");
        primary.WriteCharacteristicUuid.Should().Be("49535343-8841-43f4-a8d4-ecbe34729bb3");
        primary.NotifyCharacteristicUuid.Should().Be("49535343-1e4d-4bd9-ba61-23c647249616");
    }

    [Fact]
    public void ProtocolConstants_ShouldHaveSafeBleDefaults()
    {
        RongtaProtocol.TargetMtu.Should().Be(512);
        RongtaProtocol.FallbackChunkSize.Should().Be(20);
        RongtaProtocol.MaxSafeChunkSize.Should().Be(180);
        RongtaProtocol.DeviceNamePrefix.Should().Be("RPP30");
        RongtaProtocol.WriteRetryCount.Should().Be(3);
    }
}
