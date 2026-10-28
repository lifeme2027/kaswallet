using KasWallet;
using KasWallet.Core.Consensus;
using Xunit;

namespace KasWallet.Core.Tests;

/// <summary><see cref="ConsensusTransactionFactory.BuildSweep"/>: spend-everything transactions. Pure offline tests.</summary>
public class SweepBuilderTests
{
    private const string Destination = "kaspatest:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqhqrxplya";
    private const long Kas = 100_000_000;

    private static byte[] Key()
    {
        var secret = new byte[32];
        secret[31] = 1;
        return secret;
    }

    private static string From() => ConsensusTransactionFactory.AddressFromPrivateKey(Key(), KaspaNetwork.Testnet10);

    private static KasWallet.UtxoEntry Utxo(int n, long amount)
        => new(From(), n.ToString("x4") + new string('0', 60), 0, amount, false, 1);

    private static ScriptPublicKey ScriptOf(string address)
    {
        Assert.True(AddressValidator.TryDecode(address, out var info));
        return new ScriptPublicKey(0, ConsensusSigner.PayToPubKeyScript(info.Payload));
    }

    private static List<KasWallet.UtxoEntry> Utxos(params long[] amounts)
        => amounts.Select((a, i) => Utxo(i + 1, a)).ToList();

    [Fact]
    public void Single_utxo_is_sent_whole_minus_the_mass_fee_with_no_change()
    {
        var r = ConsensusTransactionFactory.BuildSweep(Utxos(5 * Kas), Destination, Key());

        Assert.Equal(1, r.InputsUsed);
        Assert.Equal(0, r.LeftoverInputs);
        Assert.Single(r.Built.Transaction.Outputs);
        Assert.Equal(0UL, r.Built.ChangeSompi);
        Assert.Equal(r.Built.Mass.FeeMass * 100, r.Built.FeeSompi);
        Assert.Equal((ulong)(5 * Kas) - r.Built.FeeSompi, r.SentSompi);
        Assert.Equal(r.SentSompi, r.Built.Transaction.Outputs[0].Value);
    }

    [Fact]
    public void Every_input_is_spent_and_value_is_conserved()
    {
        var r = ConsensusTransactionFactory.BuildSweep(Utxos(3 * Kas, 2 * Kas, 7 * Kas), Destination, Key());

        Assert.Equal(3, r.InputsUsed);
        Assert.Equal(3, r.Built.Transaction.Inputs.Count);
        Assert.Equal(12UL * Kas, r.SentSompi + r.Built.FeeSompi);
        Assert.All(r.Built.Transaction.Inputs, i => Assert.Equal(66, i.SignatureScript.Length));
    }

    [Fact]
    public void Reported_mass_matches_a_recomputation_on_the_signed_transaction()
    {
        var r = ConsensusTransactionFactory.BuildSweep(Utxos(3 * Kas, 2 * Kas), Destination, Key());
        var script = ScriptOf(From());
        var spent = new[]
        {
            new KasWallet.Core.Consensus.UtxoEntry(3 * Kas, script, 1, false),
            new KasWallet.Core.Consensus.UtxoEntry(2 * Kas, script, 1, false)
        };
        Assert.Equal(r.Built.Mass, MassCalculator.Calculate(r.Built.Transaction, spent));
        Assert.True(r.Built.FeeSompi >= r.Built.Mass.FeeMass * 100);
    }

    [Fact]
    public void Higher_fee_rate_scales_the_fee_and_shrinks_the_output()
    {
        var at100 = ConsensusTransactionFactory.BuildSweep(Utxos(5 * Kas), Destination, Key(), 100);
        var at300 = ConsensusTransactionFactory.BuildSweep(Utxos(5 * Kas), Destination, Key(), 300);
        Assert.Equal(at100.Built.FeeSompi * 3, at300.Built.FeeSompi);
        Assert.True(at300.SentSompi < at100.SentSompi);
    }

    [Fact]
    public void Fee_override_pays_exactly_that_fee()
    {
        var r = ConsensusTransactionFactory.BuildSweep(Utxos(5 * Kas), Destination, Key(), feeOverrideSompi: 50_000);
        Assert.Equal(50_000UL, r.Built.FeeSompi);
        Assert.Equal(5UL * Kas - 50_000, r.SentSompi);
    }

    [Fact]
    public void Too_many_inputs_are_split_off_at_the_standard_mass_limit()
    {
        var r = ConsensusTransactionFactory.BuildSweep(Utxos(Enumerable.Repeat(10 * Kas, 200).ToArray()), Destination, Key());

        Assert.True(r.InputsUsed is > 10 and < 200);
        Assert.Equal(200, r.InputsUsed + r.LeftoverInputs);
        Assert.Equal((long)r.LeftoverInputs * 10 * Kas, r.LeftoverSompi);
        Assert.True(r.Built.Mass.FeeMass <= MassCalculator.MaxStandardTransactionMass);
        // One more input (< 2000 mass each) would not have fit.
        Assert.True(r.Built.Mass.FeeMass + 2_000 > MassCalculator.MaxStandardTransactionMass);
    }

    [Fact]
    public void Largest_utxos_are_taken_first_when_not_all_fit()
    {
        var amounts = Enumerable.Range(1, 150).Select(i => (long)i * Kas).ToArray();
        var r = ConsensusTransactionFactory.BuildSweep(Utxos(amounts), Destination, Key());
        Assert.True(r.LeftoverInputs > 0);
        var spentTotal = r.SentSompi + r.Built.FeeSompi;
        var expectedTop = amounts.OrderByDescending(a => a).Take(r.InputsUsed).Sum();
        Assert.Equal((ulong)expectedTop, spentTotal);
        Assert.Equal(amounts.Sum() - expectedTop, r.LeftoverSompi);
    }

    [Fact]
    public void Max_inputs_caps_the_selection()
    {
        var r = ConsensusTransactionFactory.BuildSweep(Utxos(4 * Kas, 3 * Kas, 2 * Kas), Destination, Key(), maxInputs: 2);
        Assert.Equal(2, r.InputsUsed);
        Assert.Equal(1, r.LeftoverInputs);
        Assert.Equal(2 * Kas, r.LeftoverSompi);
        Assert.Equal(7UL * Kas, r.SentSompi + r.Built.FeeSompi);
    }

    [Fact]
    public void An_input_worth_less_than_its_own_fee_is_left_out()
    {
        // 1_000 sompi cannot pay for the ~1.1k mass (>= 110k sompi) another input adds.
        var r = ConsensusTransactionFactory.BuildSweep(Utxos(5 * Kas, 1_000), Destination, Key());
        Assert.Equal(1, r.InputsUsed);
        Assert.Equal(1, r.LeftoverInputs);
        Assert.Equal(1_000, r.LeftoverSompi);
    }

    [Fact]
    public void Zero_value_utxos_are_ignored()
    {
        var r = ConsensusTransactionFactory.BuildSweep(Utxos(0, 5 * Kas, 0), Destination, Key());
        Assert.Equal(1, r.InputsUsed);
        Assert.Equal(0, r.LeftoverInputs);
    }

    [Fact]
    public void No_utxos_is_an_error()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => ConsensusTransactionFactory.BuildSweep(Utxos(), Destination, Key()));
        Assert.Contains("No spendable", ex.Message);
    }

    [Fact]
    public void Balance_below_the_fee_is_an_error()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => ConsensusTransactionFactory.BuildSweep(Utxos(10_000), Destination, Key()));
        Assert.Contains("Insufficient funds", ex.Message);
    }

    [Fact]
    public void Argument_validation()
    {
        var u = Utxos(5 * Kas);
        Assert.Throws<ArgumentOutOfRangeException>(() => ConsensusTransactionFactory.BuildSweep(u, Destination, Key(), 99));
        Assert.Throws<ArgumentOutOfRangeException>(() => ConsensusTransactionFactory.BuildSweep(u, Destination, Key(), maxInputs: 0));
        Assert.Throws<ArgumentException>(() => ConsensusTransactionFactory.BuildSweep(u, "not-an-address", Key()));
        Assert.Throws<ArgumentException>(() => ConsensusTransactionFactory.BuildSweep(u, Destination, new byte[31]));
        Assert.Throws<ArgumentOutOfRangeException>(() => ConsensusTransactionFactory.BuildSweep(Utxos(-1), Destination, Key()));
    }

    [Fact]
    public void Signed_transaction_serializes_to_rest_json()
    {
        var r = ConsensusTransactionFactory.BuildSweep(Utxos(5 * Kas, 2 * Kas), Destination, Key());
        using var doc = System.Text.Json.JsonDocument.Parse(TransactionJson.ToRestSubmitJson(r.Built.Transaction));
        var tx = doc.RootElement.GetProperty("transaction");
        Assert.Equal(2, tx.GetProperty("inputs").GetArrayLength());
        Assert.Equal(1, tx.GetProperty("outputs").GetArrayLength());
    }
}
