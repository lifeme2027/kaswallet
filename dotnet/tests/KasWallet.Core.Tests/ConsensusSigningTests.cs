using KasWallet.Core.Consensus;
using NBitcoin.Secp256k1;
using Xunit;

namespace KasWallet.Core.Tests;

public class ConsensusSigningTests
{
    [Fact]
    public void Blake2b_matches_rfc7693_abc_and_empty_256()
    {
        var abc = Blake2b.Hash("abc"u8, 64);
        Assert.Equal(
            "ba80a53f981c4d0d6a2797b69f12f6e94c212f14685ac4b74b12bb6fdbffa2d17d87c5392aab792dc252d5de4533cc9518d38aa8dbf1925ab92386edd4009923",
            Convert.ToHexString(abc).ToLowerInvariant());

        var empty = Blake2b.Hash(ReadOnlySpan<byte>.Empty, 32);
        Assert.Equal(
            "0e5751c026e543b2e8ab2eb06099daa1d1e5df47778f7787faab45cdf12fe3a8",
            Convert.ToHexString(empty).ToLowerInvariant());
    }

    [Fact]
    public void Blake2b_keyed_and_multiblock_match_reference()
    {
        var keyed = Blake2b.Hash("abc"u8, 32, "TransactionSigningHash"u8);
        Assert.Equal(
            "1d25f7b19571ef69f5a7fac4494f74bb9c410ec485570beefe5a7e8d11fcc465",
            Convert.ToHexString(keyed).ToLowerInvariant());

        var multi = new byte[200];
        for (var i = 0; i < multi.Length; i++)
            multi[i] = (byte)i;
        Assert.Equal(
            "63c3d97a9f8894d5e043a707b0fee7f7ec4c049a23bbf1079df20b4165f9e22d",
            Convert.ToHexString(Blake2b.Hash(multi, 32)).ToLowerInvariant());
        Assert.Equal(
            "160ae16e5f0bc71d5815d7f5f42bd36fa2b4dc23e1c8dc3b59144821d083cd58",
            Convert.ToHexString(Blake2b.Hash(multi, 32, "TransactionSigningHash"u8)).ToLowerInvariant());
    }

    [Fact]
    public void Sighash_is_deterministic_and_changes_with_the_output()
    {
        var tx = Sample();
        var utxo = UtxoFor(tx, 0);
        var first = SigHash.CalcSchnorrSignatureHash(tx, 0, utxo);
        var second = SigHash.CalcSchnorrSignatureHash(tx, 0, utxo);
        Assert.Equal(32, first.Length);
        Assert.Equal(first, second);
        Assert.NotEqual(first, SigHash.CalcSchnorrSignatureHash(tx, 1, UtxoFor(tx, 1)));

        tx.Outputs[0].Value++;
        Assert.NotEqual(first, SigHash.CalcSchnorrSignatureHash(tx, 0, utxo));
    }

    [Fact]
    public void Native_empty_payload_sighash_differs_from_payload_and_non_native()
    {
        var spk = new ScriptPublicKey(0, new byte[] { 0x51 });
        var native = SigHash.CalcSchnorrSignatureHash(Sample(spk), 0, new KasWallet.Core.Consensus.UtxoEntry(1, spk, 0, false));

        var withPayload = Sample(spk, payload: new byte[] { 1 });
        Assert.NotEqual(native, SigHash.CalcSchnorrSignatureHash(withPayload, 0, new KasWallet.Core.Consensus.UtxoEntry(1, spk, 0, false)));

        var subnetwork = new byte[20];
        subnetwork[19] = 1;
        var nonNative = Sample(spk, subnetwork);
        Assert.NotEqual(native, SigHash.CalcSchnorrSignatureHash(nonNative, 0, new KasWallet.Core.Consensus.UtxoEntry(1, spk, 0, false)));
    }

    [Fact]
    public void Sign_all_builds_signature_scripts_that_verify_bip340()
    {
        var secret = new byte[32];
        secret[31] = 1;
        Assert.True(ECPrivKey.TryCreate(secret, out var priv));
        using (priv)
        {
            var xOnly = priv!.CreateXOnlyPubKey();
            var p2pk = ConsensusSigner.PayToPubKeyScript(xOnly.ToBytes());
            Assert.Equal(34, p2pk.Length);
            Assert.Equal(0x20, p2pk[0]);
            Assert.Equal(0xac, p2pk[33]);

            var spk = new ScriptPublicKey(0, p2pk);
            var tx = Sample(spk);
            var utxos = tx.Inputs.Select((_, i) => UtxoFor(tx, i, spk)).ToList();
            var sighashBefore = SigHash.CalcSchnorrSignatureHash(tx, 0, utxos[0]);

            ConsensusSigner.SignAll(tx, utxos, secret);

            Assert.Equal(sighashBefore, SigHash.CalcSchnorrSignatureHash(tx, 0, utxos[0]));
            for (var i = 0; i < tx.Inputs.Count; i++)
            {
                var script = tx.Inputs[i].SignatureScript;
                Assert.Equal(66, script.Length);
                Assert.Equal(0x41, script[0]);
                Assert.Equal(SigHash.SigHashAll, script[65]);

                var hash = SigHash.CalcSchnorrSignatureHash(tx, i, utxos[i]);
                Assert.True(SecpSchnorrSignature.TryCreate(script.AsSpan(1, 64), out var signature));
                Assert.NotNull(signature);
                Assert.True(xOnly.SigVerifyBIP340(signature, hash));

                hash[0] ^= 0xff;
                Assert.False(xOnly.SigVerifyBIP340(signature, hash));
            }
        }
    }

    [Fact]
    public void Transaction_id_ignores_signature_scripts()
    {
        var tx = Sample();
        tx.Inputs[1].SignatureScript = new byte[] { 9, 9, 9 };
        var id = SigHash.TransactionId(tx);
        Assert.Equal(32, id.Length);

        ConsensusSigner.SignAll(tx, tx.Inputs.Select((_, i) => UtxoFor(tx, i)).ToList(), KeyOne());
        Assert.Equal(id, SigHash.TransactionId(tx));

        tx.Inputs[0].SignatureScript = new byte[] { 1, 2, 3, 4, 5 };
        tx.Inputs[1].SignatureScript = Array.Empty<byte>();
        Assert.Equal(id, SigHash.TransactionId(tx));

        var input = tx.Inputs[0];
        tx.Inputs[0] = new TransactionInput(input.PreviousOutpoint, input.SignatureScript, input.Sequence + 1, input.SigOpCount);
        Assert.NotEqual(id, SigHash.TransactionId(tx));
    }

    private static byte[] KeyOne()
    {
        var secret = new byte[32];
        secret[31] = 1;
        return secret;
    }

    private static KasWallet.Core.Consensus.UtxoEntry UtxoFor(Transaction tx, int index, ScriptPublicKey? spk = null)
    {
        spk ??= tx.Outputs[0].ScriptPublicKey;
        return new KasWallet.Core.Consensus.UtxoEntry(1_000_000UL + (ulong)index, spk, blockDaaScore: 50, isCoinbase: false);
    }

    private static Transaction Sample(ScriptPublicKey? spk = null, byte[]? subnetworkId = null, byte[]? payload = null)
    {
        spk ??= new ScriptPublicKey(0, new byte[] { 0x51 });
        var inputs = new[]
        {
            new TransactionInput(new TransactionOutpoint(Id(1), 0), Array.Empty<byte>(), sequence: 1, sigOpCount: 1),
            new TransactionInput(new TransactionOutpoint(Id(2), 3), new byte[] { 9, 9 }, sequence: 2, sigOpCount: 1),
        };
        var outputs = new[]
        {
            new TransactionOutput(500, spk),
            new TransactionOutput(400, spk),
        };
        return new Transaction(0, inputs, outputs, lockTime: 7, subnetworkId ?? new byte[20], gas: 0, payload ?? Array.Empty<byte>());
    }

    private static byte[] Id(byte marker)
    {
        var id = new byte[32];
        id[0] = marker;
        return id;
    }
}
