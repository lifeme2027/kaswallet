using KasWallet;
using KasWallet.Core.Consensus;
using Xunit;

namespace KasWallet.Core.Tests;

/// <summary>Mass-based fee selection in <see cref="ConsensusTransactionFactory.BuildWithFeeRate"/>.</summary>
public class FeeRateBuilderTests
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

    private static string Id(byte marker) => marker.ToString("x2") + new string('0', 62);

    private static KasWallet.UtxoEntry Utxo(byte marker, long amount)
        => new(From(), Id(marker), 0, amount, false, 1);

    private static BuiltTransaction Build(long amount, ulong rate, params long[] utxoAmounts)
    {
        var utxos = utxoAmounts.Select((a, i) => Utxo((byte)(i + 1), a)).ToList();
        return ConsensusTransactionFactory.BuildWithFeeRate(utxos, Destination, amount, From(), Key(), rate);
    }

    [Fact]
    public void Five_kas_from_a_large_utxo_pays_compute_mass_2036_at_minimum_rate()
    {
        var built = Build(5 * Kas, 100, 5_675_155_239);
        Assert.Equal(2036UL, built.Mass.FeeMass);
        Assert.Equal(2036UL * 100, built.FeeSompi); // 0.002036 KAS
        Assert.Equal(100UL, built.FeeRate);
        Assert.Equal(2, built.Transaction.Outputs.Count);
        Assert.Equal(5_675_155_239UL - 5UL * Kas - built.FeeSompi, built.ChangeSompi);
    }

    [Fact]
    public void One_kas_from_a_large_utxo_is_dominated_by_storage_mass()
    {
        var built = Build(1 * Kas, 100, 5_675_155_239);
        Assert.Equal(built.Mass.StorageMass, built.Mass.FeeMass);
        Assert.InRange(built.Mass.FeeMass, 10_000UL, 10_010UL); // C / 1e8 = 10_000 plus change term
        Assert.Equal(built.Mass.FeeMass * 100, built.FeeSompi);
        Assert.InRange(built.FeeSompi, 1_000_000UL, 1_001_000UL); // ~0.01 KAS
    }

    [Fact]
    public void Small_output_of_0_12_kas_pays_about_0_083_kas()
    {
        var built = Build(12_000_000, 100, 5_675_155_239);
        Assert.InRange(built.Mass.FeeMass, 83_333UL, 83_340UL);
        Assert.Equal(built.Mass.FeeMass * 100, built.FeeSompi);
        Assert.InRange(built.FeeSompi, 8_333_300UL, 8_334_000UL);
    }

    [Fact]
    public void Fee_equals_mass_times_rate_for_higher_rates()
    {
        var at100 = Build(5 * Kas, 100, 5_675_155_239);
        var at300 = Build(5 * Kas, 300, 5_675_155_239);
        Assert.Equal(at100.Mass.FeeMass, at300.Mass.FeeMass);
        Assert.Equal(at100.FeeSompi * 3, at300.FeeSompi);
        Assert.Equal(300UL, at300.FeeRate);
    }

    [Fact]
    public void Reported_mass_matches_a_recomputation_on_the_signed_transaction()
    {
        var built = Build(5 * Kas, 100, 5_675_155_239);
        var script = built.Transaction.Outputs[1].ScriptPublicKey;
        var spent = new[] { new KasWallet.Core.Consensus.UtxoEntry(5_675_155_239, script, 1, false) };
        var again = MassCalculator.Calculate(built.Transaction, spent);
        Assert.Equal(built.Mass, again);
        Assert.All(built.Transaction.Inputs, i => Assert.Equal(66, i.SignatureScript.Length));
    }

    [Fact]
    public void Multiple_inputs_are_added_largest_first_and_each_adds_mass()
    {
        var built = Build(5 * Kas, 100, 3 * Kas, 3 * Kas, 3 * Kas);
        Assert.Equal(2, built.Transaction.Inputs.Count); // 3 KAS alone is not enough for 5 KAS + fee
        var one = Build(Kas, 100, 3 * Kas);
        Assert.Single(one.Transaction.Inputs);
        Assert.True(built.Mass.ComputeMass > one.Mass.ComputeMass + 1000);
        Assert.Equal(built.Mass.FeeMass * 100, built.FeeSompi);
        var outTotal = built.Transaction.Outputs.Sum(o => (long)o.Value);
        Assert.Equal(6 * Kas, outTotal + (long)built.FeeSompi);
    }

    [Fact]
    public void No_utxos_is_an_error()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Build(Kas, 100));
        Assert.Contains("No spendable", ex.Message);
    }

    [Fact]
    public void Insufficient_funds_counts_the_fee()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Build(Kas, 100, Kas));
        Assert.Contains("Insufficient funds", ex.Message);
    }

    [Fact]
    public void Fee_rate_below_the_minimum_is_rejected()
        => Assert.Throws<ArgumentOutOfRangeException>(() => Build(Kas, 99, 5 * Kas));

    [Fact]
    public void Tiny_change_that_explodes_storage_mass_is_refused()
    {
        // Leaves a few thousand sompi of change: storage mass is far above the 100k standard limit.
        Assert.Throws<InvalidOperationException>(() => Build(Kas, 100, Kas + 1_002_000));
    }

    [Fact]
    public void Fee_override_pays_exactly_that_fee_but_still_reports_mass()
    {
        var utxos = new[] { Utxo(1, 5_675_155_239) };
        var built = ConsensusTransactionFactory.BuildWithFeeRate(utxos, Destination, 5 * Kas, From(), Key(), 100, feeOverrideSompi: 50_000);
        Assert.Equal(50_000UL, built.FeeSompi);
        Assert.Equal(2036UL, built.Mass.FeeMass);
        Assert.Contains(FeeReport.Lines(built), l => l.StartsWith("warning=", StringComparison.Ordinal));
    }

    [Fact]
    public void Fee_report_lists_mass_rate_and_fee()
    {
        var built = Build(5 * Kas, 100, 5_675_155_239);
        var lines = FeeReport.Lines(built);
        Assert.Contains("mass=2036", lines);
        Assert.Contains("fee_rate=100", lines);
        Assert.Contains("fee_sompi=203600", lines);
        Assert.Contains("fee_kas=0.00203600", lines);
        Assert.DoesNotContain(lines, l => l.StartsWith("warning=", StringComparison.Ordinal));
    }
}
