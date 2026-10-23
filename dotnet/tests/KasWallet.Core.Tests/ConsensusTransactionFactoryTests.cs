using System.Text.Json;
using KasWallet;
using KasWallet.Core.Consensus;
using NBitcoin.Secp256k1;
using Xunit;

namespace KasWallet.Core.Tests;

public class ConsensusTransactionFactoryTests
{
    private const string Mnemonic =
        "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

    private const string Destination = "kaspatest:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqhqrxplya";

    [Fact]
    public void Builds_change_output_from_the_largest_sufficient_utxo_and_signs()
    {
        var key = Key();
        var from = ConsensusTransactionFactory.AddressFromPrivateKey(key, KaspaNetwork.Testnet10);
        var small = DisplayId(0x01);
        var large = DisplayId(0x02);
        var utxos = new[]
        {
            new UtxoEntry(from, small, 0, 1_000, false, 1),
            new UtxoEntry(from, large, 4, 5_000, false, 2)
        };

        var tx = ConsensusTransactionFactory.Build(utxos, Destination, amountSompi: 1_200, feeSompi: 300, from, key);

        Assert.Equal(0, tx.Version);
        Assert.Equal(0UL, tx.LockTime);
        Assert.Equal(0UL, tx.Gas);
        Assert.Empty(tx.Payload);
        Assert.Equal(new byte[20], tx.SubnetworkId);
        Assert.Single(tx.Inputs);
        Assert.Equal(4u, tx.Inputs[0].PreviousOutpoint.Index);
        Assert.Equal(0UL, tx.Inputs[0].Sequence);
        Assert.Equal(1, tx.Inputs[0].SigOpCount);
        Assert.Equal(Convert.FromHexString(large), tx.Inputs[0].PreviousOutpoint.TransactionId);
        Assert.Equal(large, TransactionJson.FormatTransactionId(tx.Inputs[0].PreviousOutpoint.TransactionId));

        Assert.Equal(2, tx.Outputs.Count);
        Assert.Equal(1_200UL, tx.Outputs[0].Value);
        Assert.Equal(3_500UL, tx.Outputs[1].Value);
        Assert.Equal(Script(Destination), tx.Outputs[0].ScriptPublicKey.Script);
        Assert.Equal(Script(from), tx.Outputs[1].ScriptPublicKey.Script);
        Assert.Equal(5_000UL, tx.Outputs[0].Value + tx.Outputs[1].Value + 300UL);

        var scriptSig = tx.Inputs[0].SignatureScript;
        Assert.Equal(66, scriptSig.Length);
        Assert.Equal(0x41, scriptSig[0]);
        Assert.Equal(SigHash.SigHashAll, scriptSig[^1]);
        var spent = new KasWallet.Core.Consensus.UtxoEntry(5_000, new ScriptPublicKey(0, Script(from)), 2, false);
        var hash = SigHash.CalcSchnorrSignatureHash(tx, 0, spent);
        Assert.True(ECPrivKey.TryCreate(key, out var priv));
        using (priv)
        {
            Assert.True(SecpSchnorrSignature.TryCreate(scriptSig.AsSpan(1, 64), out var signature));
            Assert.True(priv!.CreateXOnlyPubKey().SigVerifyBIP340(signature, hash));
        }

        var json = TransactionJson.ToRestSubmitJson(tx);
        using var doc = JsonDocument.Parse(json);
        var outputs = doc.RootElement.GetProperty("transaction").GetProperty("outputs");
        Assert.Equal(JsonValueKind.Number, outputs[0].GetProperty("amount").ValueKind);
        Assert.Equal(1_200UL, outputs[0].GetProperty("amount").GetUInt64());
        Assert.Equal(3_500UL, outputs[1].GetProperty("amount").GetUInt64());
        Assert.Equal(large, doc.RootElement.GetProperty("transaction").GetProperty("inputs")[0]
            .GetProperty("previousOutpoint").GetProperty("transactionId").GetString());
    }

    [Fact]
    public void Omits_change_when_inputs_equal_amount_plus_fee()
    {
        var key = Key();
        var from = ConsensusTransactionFactory.AddressFromPrivateKey(key, KaspaNetwork.Testnet10);
        var utxos = new[] { new UtxoEntry(from, DisplayId(0x03), 0, 1_000, false, 1) };

        var tx = ConsensusTransactionFactory.Build(utxos, Destination, 900, 100, from, key);

        Assert.Single(tx.Outputs);
        Assert.Equal(900UL, tx.Outputs[0].Value);
    }

    [Fact]
    public void Rejects_insufficient_funds_without_network()
    {
        var key = Key();
        var from = ConsensusTransactionFactory.AddressFromPrivateKey(key, KaspaNetwork.Testnet10);
        var utxos = new[] { new UtxoEntry(from, DisplayId(0x04), 0, 100, false, 1) };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ConsensusTransactionFactory.Build(utxos, Destination, 80, 30, from, key));
        Assert.Contains("Insufficient", ex.Message);
        Assert.Contains("100", ex.Message);
        Assert.Contains("110", ex.Message);
    }

    [Fact]
    public void Signing_key_accepts_hex_or_mnemonic_derivation()
    {
        var key = Key();
        var hex = "0x" + Convert.ToHexString(key).ToLowerInvariant();
        Assert.Equal(key, ConsensusTransactionFactory.SigningKeyFrom(hex));

        var derived = ConsensusTransactionFactory.SigningKeyFrom(Mnemonic);
        var address = ConsensusTransactionFactory.AddressFromPrivateKey(derived, KaspaNetwork.Mainnet);
        Assert.Equal(SchnorrAddress.GenerateReceiveAddress(Mnemonic, 0, KaspaNetwork.Mainnet), address);
    }

    private static byte[] Key()
    {
        var secret = new byte[32];
        secret[31] = 1;
        return secret;
    }

    private static string DisplayId(byte marker) => marker.ToString("x2") + new string('0', 62);

    private static byte[] Script(string address) => Convert.FromHexString(SchnorrAddress.ScriptForAddress(address));
}
