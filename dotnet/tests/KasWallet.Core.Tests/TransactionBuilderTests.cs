using KasWallet;
using Xunit;

namespace KasWallet.Core.Tests;

public class TransactionBuilderTests
{
    [Fact]
    public void Builds_unsigned_plan_with_change()
    {
        var utxos = new[]
        {
            new UtxoEntry("kaspatest:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4e", "aa", 0, 5_0000_0000, false, 1)
        };
        var plan = TransactionBuilder.BuildPayment(
            KaspaNetwork.Testnet10,
            utxos,
            "kaspatest:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4e",
            1_0000_0000,
            "kaspatest:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4e",
            10_000);
        Assert.Single(plan.SelectedInputs);
        Assert.True(plan.ChangeSompi > 0);
        Assert.Contains("Unsigned", plan.Notes);
    }
}
