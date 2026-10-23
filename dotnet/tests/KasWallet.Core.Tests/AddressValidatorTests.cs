using KasWallet;
using Xunit;

namespace KasWallet.Core.Tests;

public class AddressValidatorTests
{
    private const string MainnetZero = "kaspa:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4e";
    private const string TestnetZero = "kaspatest:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqhqrxplya";

    [Theory]
    [InlineData("kaspa:qz")]
    [InlineData("not-an-address")]
    [InlineData("kaspa:")]
    [InlineData("btc:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq")]
    [InlineData("kaspa:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4l")]
    [InlineData("kaspatest:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4e")]
    public void Rejects_invalid(string address) => Assert.False(AddressValidator.IsValid(address));

    [Fact]
    public void Rejects_bad_checksum_explicitly()
    {
        var address = "kaspa:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4l";
        Assert.Equal(AddressCheck.BadChecksum, AddressValidator.Check(address));
    }

    [Fact]
    public void Accepts_testnet_zero_pubkey()
    {
        Assert.True(AddressValidator.IsValid(TestnetZero));
        Assert.True(AddressValidator.TryDetectNetwork(TestnetZero, out var net));
        Assert.Equal(KaspaNetwork.Testnet10, net);
        Assert.True(AddressValidator.TryDecode(TestnetZero, out var info));
        Assert.Equal(0, info.Version);
        Assert.Equal(32, info.Payload.Length);
        Assert.All(info.Payload, b => Assert.Equal(0, b));
    }

    [Fact]
    public void Detects_mainnet_prefix()
    {
        Assert.True(AddressValidator.TryDetectNetwork(MainnetZero, out var net));
        Assert.Equal(KaspaNetwork.Mainnet, net);
        Assert.Equal(AddressCheck.Valid, AddressValidator.Check(MainnetZero));
    }
}
