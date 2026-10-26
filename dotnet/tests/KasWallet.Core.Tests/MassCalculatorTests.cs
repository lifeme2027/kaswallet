using KasWallet.Core.Consensus;
using Xunit;
using SpentEntry = KasWallet.Core.Consensus.UtxoEntry;

namespace KasWallet.Core.Tests;

/// <summary>
/// Vectors marked "rusty-kaspa" are copied from the unit tests in
/// https://github.com/kaspanet/rusty-kaspa/blob/master/consensus/core/src/mass/mod.rs
/// (<c>test_storage_mass</c>, <c>test_storage_mass_pluralities</c>, <c>verify_utxo_plurality_limits</c>,
/// <c>test_mass_cofactors</c>, <c>test_normalized_max_ranking</c>). Vectors marked "api.kaspa.org" were
/// measured against api.kaspa.org <c>/transactions/mass</c> and are not part of the upstream test suite.
/// </summary>
public class MassCalculatorTests
{
    private const ulong C12 = 1_000_000_000_000UL;
    private const ulong Kas = 100_000_000UL;

    private static ulong Storage(ulong[] ins, ulong[] outs, ulong c = MassCalculator.StorageMassParameter)
        => MassCalculator.StorageMass(
            ins.Select(a => new UtxoCell(1, a)).ToList(),
            outs.Select(a => new UtxoCell(1, a)).ToList(),
            c);

    // ---- constants -------------------------------------------------------------------------

    [Fact]
    public void Constants_match_rusty_kaspa()
    {
        Assert.Equal(1_000_000_000_000UL, MassCalculator.StorageMassParameter); // SOMPI_PER_KASPA * 10_000
        Assert.Equal(4UL, MassCalculator.TransientByteToMassFactor);
        Assert.Equal(1UL, MassCalculator.MassPerTxByte);
        Assert.Equal(10UL, MassCalculator.MassPerScriptPubKeyByte);
        Assert.Equal(1000UL, MassCalculator.MassPerSigOp);
        Assert.Equal(new BlockMassLimits(500_000, 500_000, 1_000_000), BlockMassLimits.Default);
    }

    // ---- storage mass: rusty-kaspa test_storage_mass --------------------------------------------

    [Fact]
    public void Storage_3in_2out_symmetric_is_zero_rusty_kaspa()
        => Assert.Equal(0UL, Storage(new ulong[] { 100, 200, 300 }, new ulong[] { 300, 300 }, C12));

    [Fact]
    public void Storage_3in_2out_asymmetric_follows_formula_rusty_kaspa()
    {
        var actual = Storage(new ulong[] { 100, 200, 300 }, new ulong[] { 50, 550 }, C12);
        Assert.Equal(C12 / 50 + C12 / 550 - 3 * (C12 / 200), actual);
    }

    [Fact]
    public void Storage_more_outputs_than_inputs_rusty_kaspa()
    {
        var b = 10_000 * Kas;
        // Inputs are above C so they do not contribute negative mass; 4 outputs equal to C each charge 1.
        Assert.Equal(4UL, Storage(new[] { b, b, b * 2 }, new[] { b, b, b, b }));
        Assert.Equal(1003UL, Storage(new[] { b, b, b * 2 }, new[] { 10 * Kas, b, b, b }));
        // Increase every value over the limit: mass vanishes.
        Assert.Equal(0UL, Storage(new[] { b + 4, b, b * 2 }, new[] { b + 1, b + 1, b + 1, b + 1 }));
    }

    [Fact]
    public void Storage_2in_2out_relaxed_formula_rusty_kaspa()
    {
        Assert.Equal(9_000_000_000UL, Storage(new ulong[] { 100, 200 }, new ulong[] { 50, 250 }, C12));
        Assert.Equal(0UL, Storage(new ulong[] { 100, 200 }, new ulong[] { 100, 200 }, C12));
        Assert.Equal(5_000_000_000UL, Storage(new ulong[] { 100, 200 }, new ulong[] { 50 }, C12));
    }

    // ---- storage mass: rusty-kaspa test_storage_mass_pluralities -------------------------------

    public static IEnumerable<object[]> PluralityCases()
    {
        // name, ins1, outs1, ins2, outs2, index, plurality, overrideOutput
        yield return new object[] { "3:4 input[1] p=2", new ulong[] { 300, 200, 200 }, new ulong[] { 200, 200, 200, 100 }, new ulong[] { 300, 400 }, new ulong[] { 200, 200, 200, 100 }, 1, 2, false };
        yield return new object[] { "2:3 output[1] p=2", new ulong[] { 350, 400 }, new ulong[] { 300, 200, 200 }, new ulong[] { 350, 400 }, new ulong[] { 300, 400 }, 1, 2, true };
        yield return new object[] { "1:2 output[0] p=2", new ulong[] { 500 }, new ulong[] { 200, 200 }, new ulong[] { 500 }, new ulong[] { 400 }, 0, 2, true };
        yield return new object[] { "1:3 output[1] p=2", new ulong[] { 1000 }, new ulong[] { 200, 200, 200 }, new ulong[] { 1000 }, new ulong[] { 200, 400 }, 1, 2, true };
        yield return new object[] { "1:3 output[1] p=2 kas", new[] { 1000 * Kas }, new[] { 200 * Kas, 200 * Kas, 200 * Kas }, new[] { 1000 * Kas }, new[] { 200 * Kas, 400 * Kas }, 1, 2, true };
        yield return new object[] { "1:2 output[0] p=2 kas", new[] { 1000 * Kas }, new[] { 200 * Kas, 200 * Kas }, new[] { 1000 * Kas }, new[] { 400 * Kas }, 0, 2, true };
        yield return new object[] { "2:2 output[0] p=2 kas", new[] { 350 * Kas, 500 * Kas }, new[] { 200 * Kas, 200 * Kas }, new[] { 350 * Kas, 500 * Kas }, new[] { 400 * Kas }, 0, 2, true };
        yield return new object[]
        {
            "4:6 output[3] p=3 kas",
            new[] { 350 * Kas, 500 * Kas, 350 * Kas, 500 * Kas },
            new[] { 200 * Kas, 200 * Kas, 400 * Kas, 250 * Kas, 250 * Kas, 250 * Kas },
            new[] { 350 * Kas, 500 * Kas, 350 * Kas, 500 * Kas },
            new[] { 200 * Kas, 200 * Kas, 400 * Kas, 750 * Kas },
            3, 3, true
        };
    }

    [Theory]
    [MemberData(nameof(PluralityCases))]
    public void Super_entry_storage_mass_equals_equivalent_plurality_one_entries_rusty_kaspa(
        string name, ulong[] ins1, ulong[] outs1, ulong[] ins2, ulong[] outs2, int index, int plurality, bool overrideOutput)
    {
        var tx1In = ins1.Select(a => new UtxoCell(1, a)).ToList();
        var tx1Out = outs1.Select(a => new UtxoCell(1, a)).ToList();
        var tx2In = ins2.Select(a => new UtxoCell(1, a)).ToList();
        var tx2Out = outs2.Select(a => new UtxoCell(1, a)).ToList();
        // A script of (p - 1) * 100 bytes yields plurality p (UTXO fixed part is 63 bytes).
        var p = MassCalculator.UtxoPlurality((plurality - 1) * 100);
        Assert.Equal((ulong)plurality, p);
        if (overrideOutput)
            tx2Out[index] = tx2Out[index] with { Plurality = p };
        else
            tx2In[index] = tx2In[index] with { Plurality = p };

        var mass1 = MassCalculator.StorageMass(tx1In, tx1Out, C12);
        var mass2 = MassCalculator.StorageMass(tx2In, tx2Out, C12);
        Assert.NotEqual(0UL, mass1); // upstream: "avoid running meaningless test cases"
        Assert.True(mass1 == mass2, $"{name}: {mass1} != {mass2}");
    }

    [Fact]
    public void Utxo_plurality_boundaries_rusty_kaspa()
    {
        Assert.Equal(1UL, MassCalculator.UtxoPlurality(0));
        Assert.Equal(1UL, MassCalculator.UtxoPlurality(100 - 63));
        Assert.Equal(2UL, MassCalculator.UtxoPlurality(100 - 63 + 1));
        Assert.Equal(2UL, MassCalculator.UtxoPlurality(100 - 63, hasCovenantId: true));
        Assert.Equal(2UL, MassCalculator.UtxoPlurality(2 * 100 - 63 - 32, hasCovenantId: true));
        Assert.Equal(3UL, MassCalculator.UtxoPlurality(2 * 100 - 63 - 32 + 1, hasCovenantId: true));
        Assert.Equal(1UL, MassCalculator.UtxoPlurality(34)); // standard P2PK script
    }

    // ---- normalization: rusty-kaspa test_mass_cofactors / test_normalized_max_ranking ---------------

    [Fact]
    public void Normalized_max_ranking_rusty_kaspa()
    {
        var limits = new BlockMassLimits(Storage: 1_000_000, Compute: 500_000, Transient: 250_000);
        Assert.Equal(250_000UL, MassCalculator.NormalizedMax(0, 0, 500_000, limits)); // ceil(500_000 * 0.5)
        Assert.Equal(300_000UL, MassCalculator.NormalizedMax(300_000, 0, 0, limits));
        Assert.Equal(400_000UL, MassCalculator.NormalizedMax(0, 200_000, 0, limits)); // ceil(200_000 * 2.0)
    }

    [Theory]
    [InlineData(500_000UL, 500_000UL, 500_000UL)]
    [InlineData(1_000_000UL, 500_000UL, 250_000UL)]
    [InlineData(123_456UL, 78_901UL, 45_678UL)]
    [InlineData(333_333UL, 77_777UL, 12_345UL)]
    [InlineData(1_048_575UL, 524_287UL, 262_143UL)]
    public void A_full_dimension_normalizes_to_the_compute_limit_rusty_kaspa(ulong storage, ulong compute, ulong transient)
    {
        var l = new BlockMassLimits(storage, compute, transient);
        Assert.Equal(compute, MassCalculator.NormalizedMax(0, 0, storage, l));
        Assert.Equal(compute, MassCalculator.NormalizedMax(compute, 0, 0, l));
        Assert.Equal(compute, MassCalculator.NormalizedMax(0, transient, 0, l));
    }

    // ---- api.kaspa.org measurements --------------------------------------------------------------------

    [Fact]
    public void Storage_mass_samples_match_api_kaspa_org()
    {
        Assert.Equal(83_333UL, Storage(new ulong[] { 5_675_155_239 }, new ulong[] { 12_000_000, 5_654_655_239 }));
        Assert.Equal(10_003UL, Storage(new ulong[] { 5_675_155_239 }, new ulong[] { 100_000_000, 5_573_155_239 }));
        Assert.Equal(2_017UL, Storage(new ulong[] { 5_675_155_239 }, new ulong[] { 500_000_000, 5_171_555_239 }));
    }

    // ---- error handling and edge cases --------------------------------------------------------------------

    [Fact]
    public void Zero_inputs_is_an_error()
    {
        Assert.Throws<ArgumentException>(() => Storage(Array.Empty<ulong>(), new ulong[] { 100 }));
        var tx = Tx(Array.Empty<int>(), new ulong[] { 100 });
        Assert.Throws<ArgumentException>(() => MassCalculator.Calculate(tx, Array.Empty<SpentEntry>()));
    }

    [Fact]
    public void Zero_outputs_and_zero_amounts_are_errors()
    {
        Assert.Throws<ArgumentException>(() => Storage(new ulong[] { 100 }, Array.Empty<ulong>()));
        Assert.Throws<ArgumentException>(() => Storage(new ulong[] { 100 }, new ulong[] { 0 }));
        Assert.Throws<ArgumentException>(() => Storage(new ulong[] { 0 }, new ulong[] { 100 }));
    }

    [Fact]
    public void Spent_entry_count_must_match_inputs()
    {
        var tx = Tx(new[] { 66, 66 }, new ulong[] { 100 });
        Assert.Throws<ArgumentException>(() => MassCalculator.Calculate(tx, new[] { Entry(500) }));
    }

    [Fact]
    public void Version_1_transactions_are_not_supported()
    {
        var tx = new Transaction(1, Array.Empty<TransactionInput>(), Array.Empty<TransactionOutput>(), 0, new byte[20], 0, Array.Empty<byte>());
        Assert.Throws<NotSupportedException>(() => MassCalculator.ComputeMass(tx));
    }

    [Fact]
    public void Coinbase_has_zero_mass()
    {
        var subnet = new byte[20];
        subnet[0] = 1;
        var tx = new Transaction(0, Array.Empty<TransactionInput>(), new[] { Output(100) }, 0, subnet, 0, Array.Empty<byte>());
        var mass = MassCalculator.Calculate(tx, Array.Empty<SpentEntry>());
        Assert.Equal(0UL, mass.FeeMass);
        Assert.Equal(0UL, mass.ComputeMass);
    }

    // ---- compute / transient mass ----------------------------------------------------------------------------

    [Fact]
    public void Blank_transaction_serializes_to_94_bytes()
    {
        var tx = new Transaction(0, Array.Empty<TransactionInput>(), Array.Empty<TransactionOutput>(), 0, new byte[20], 0, Array.Empty<byte>());
        Assert.Equal(94UL, MassCalculator.EstimatedSerializedSize(tx));
        Assert.Equal(94UL, MassCalculator.ComputeMass(tx));
        Assert.Equal(94UL * 4, MassCalculator.TransientMass(tx));
    }

    [Fact]
    public void Compute_mass_of_signed_1_in_2_out_p2pk_is_2036_api_kaspa_org()
    {
        // size: 94 + input (32+4+8+66+8 = 118) + 2 outputs (8+2+8+34 = 52) = 316
        // spk:  2 outputs * (2 + 34) * 10 = 720 ; sigops: 1 * 1000
        var tx = Tx(new[] { 66 }, new ulong[] { 500_000_000, 5_000_000_000 });
        Assert.Equal(316UL, MassCalculator.EstimatedSerializedSize(tx));
        Assert.Equal(316UL + 720 + 1000, MassCalculator.ComputeMass(tx));
        Assert.Equal(2036UL, MassCalculator.ComputeMass(tx));
        Assert.Equal(316UL * 4, MassCalculator.TransientMass(tx));
    }

    [Fact]
    public void Compute_mass_scales_with_inputs_sigops_and_payload()
    {
        var one = MassCalculator.ComputeMass(Tx(new[] { 66 }, new ulong[] { 100 }));
        var three = MassCalculator.ComputeMass(Tx(new[] { 66, 66, 66 }, new ulong[] { 100 }));
        Assert.Equal(2 * (118 + 1000), (long)(three - one)); // each extra input: 118 bytes + 1000 sigop mass
        var withPayload = new Transaction(0, new[] { Input(66) }, new[] { Output(100) }, 0, new byte[20], 0, new byte[10]);
        Assert.Equal(one + 10, MassCalculator.ComputeMass(withPayload));
    }

    [Fact]
    public void Fee_mass_normalizes_transient_to_half_and_takes_the_maximum()
    {
        var tx = Tx(new[] { 66 }, new ulong[] { 500_000_000, 5_171_555_239 });
        var mass = MassCalculator.Calculate(tx, new[] { Entry(5_675_155_239) });
        Assert.Equal(2036UL, mass.ComputeMass);
        Assert.Equal(316UL * 4, mass.TransientMass);
        Assert.Equal(316UL * 2, mass.NormalizedTransientMass);
        Assert.Equal(2017UL, mass.StorageMass);
        Assert.Equal(2036UL, mass.FeeMass); // compute dominates

        var small = Tx(new[] { 66 }, new ulong[] { 12_000_000, 5_654_655_239 });
        var smallMass = MassCalculator.Calculate(small, new[] { Entry(5_675_155_239) });
        Assert.Equal(83_333UL, smallMass.FeeMass); // storage dominates
    }

    // ---- fee ------------------------------------------------------------------------------------------------------

    [Fact]
    public void Fee_is_mass_times_rate_and_rejects_rates_below_minimum()
    {
        Assert.Equal(203_600UL, MassCalculator.EstimateFee(2036, 100));
        Assert.Equal(2036UL * 250, MassCalculator.EstimateFee(2036, 250));
        Assert.Equal(0UL, MassCalculator.EstimateFee(0, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => MassCalculator.EstimateFee(2036, 99));
        Assert.Throws<OverflowException>(() => MassCalculator.EstimateFee(ulong.MaxValue, 100));
    }

    // ---- helpers ------------------------------------------------------------------------------------------------------

    private static byte[] P2pk() => ConsensusSigner.PayToPubKeyScript(new byte[32]);

    private static TransactionInput Input(int sigScriptLength, int marker = 0)
    {
        var id = new byte[32];
        id[0] = (byte)marker;
        return new TransactionInput(new TransactionOutpoint(id, (uint)marker), new byte[sigScriptLength], 0, 1);
    }

    private static TransactionOutput Output(ulong value) => new(value, new ScriptPublicKey(0, P2pk()));

    private static SpentEntry Entry(ulong amount) => new(amount, new ScriptPublicKey(0, P2pk()), 0, false);

    private static Transaction Tx(int[] sigScriptLengths, ulong[] outputValues)
        => new(0,
            sigScriptLengths.Select((len, i) => Input(len, i)).ToList(),
            outputValues.Select(Output).ToList(),
            0, new byte[20], 0, Array.Empty<byte>());
}
