using KasWallet;
using Xunit;

namespace KasWallet.Core.Tests;

public class AddressValidatorTests
{
    [Theory]
    [InlineData("kaspa:qz")]
    [InlineData("not-an-address")]
    [InlineData("kaspa:")]
    [InlineData("btc:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq")]
    public void Rejects_invalid(string address) => Assert.False(AddressValidator.IsValid(address));

    [Fact]
    public void Accepts_plausible_testnet_shape()
    {
        // charset-valid placeholder (not a known funded address)
        var addr = "kaspatest:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4e";
        Assert.True(AddressValidator.IsValid(addr));
        Assert.True(AddressValidator.TryDetectNetwork(addr, out var net));
        Assert.Equal(KaspaNetwork.Testnet10, net);
    }

    [Fact]
    public void Detects_mainnet_prefix()
    {
        var addr = "kaspa:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4e";
        Assert.True(AddressValidator.TryDetectNetwork(addr, out var net));
        Assert.Equal(KaspaNetwork.Mainnet, net);
    }
}
