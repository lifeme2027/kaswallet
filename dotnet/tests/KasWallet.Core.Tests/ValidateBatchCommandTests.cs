using KasWallet;
using KasWallet.Cli;
using Xunit;

namespace KasWallet.Core.Tests;

public class ValidateBatchCommandTests
{
    private const string Mainnet = "kaspa:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4e";
    private const string Testnet = "kaspatest:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqhqrxplya";
    private const string BadChecksum = "kaspa:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4f";

    private static (int Code, string[] Lines) Run(KaspaNetwork? net, params string[] inputs)
    {
        var w = new StringWriter();
        var code = ValidateBatchCommand.Run(inputs, net, w);
        return (code, w.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    [Fact]
    public void All_valid_returns_zero_and_summary()
    {
        var (code, lines) = Run(null, Mainnet, "  " + Testnet + "  ");
        Assert.Equal(0, code);
        Assert.Equal($"valid\t{Mainnet}", lines[0]);
        Assert.Equal($"valid\t{Testnet}", lines[1]);
        Assert.Equal("summary total=2 valid=2 invalid=0", lines[^1]);
    }

    [Fact]
    public void Mixed_input_reports_reasons_and_returns_one()
    {
        var (code, lines) = Run(null, Mainnet, BadChecksum, "nope");
        Assert.Equal(1, code);
        Assert.Equal($"invalid\t{BadChecksum}\tchecksum=bad", lines[1]);
        Assert.Equal("invalid\tnope\tmalformed", lines[2]);
        Assert.Equal("summary total=3 valid=1 invalid=2", lines[^1]);
    }

    [Fact]
    public void Expected_network_flags_mismatch()
    {
        var (code, lines) = Run(KaspaNetwork.Testnet10, Testnet, Mainnet);
        Assert.Equal(1, code);
        Assert.StartsWith("valid\t", lines[0]);
        Assert.StartsWith($"invalid\t{Mainnet}\tnetwork_mismatch(", lines[1]);
    }

    [Fact]
    public void Blank_and_comment_lines_are_skipped_and_empty_input_fails()
    {
        var (code, lines) = Run(null, "", "# comment", "   ");
        Assert.Equal(1, code);
        Assert.Equal("summary total=0 valid=0 invalid=0", Assert.Single(lines));
    }
}
