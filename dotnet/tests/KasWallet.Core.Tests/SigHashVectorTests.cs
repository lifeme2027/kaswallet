using KasWallet.Core.Consensus;
using Xunit;

namespace KasWallet.Core.Tests;

/// <summary>
/// Official vectors from rusty-kaspa consensus/core/src/hashing/sighash.rs test_signature_hash.
/// Hex is raw hash and txid bytes (not display-reversed).
/// </summary>
public class SigHashVectorTests
{
    private const string PrevTxIdHex = "880eb9819a31821d9d2399e2f35e2433b72637e393d71ecc9b8d0250f49153c3";
    private const string Spk1Hex = "208325613d2eeaf7176ac6c670b13c0043156c427438ed72d74b7800862ad884e8ac";
    private const string Spk2Hex = "20fcef4c106cf11135bbd70f02a726a92162d2fb8b22f0469126f800862ad884e8ac";

    [Theory]
    [InlineData("native", "none", "03b7ac6927b2b67100734c3cc313ff8c2e8b3ce3e746d46dd660b706a916b1f5")]
    [InlineData("native", "outpoint-index", "a9f563d86c0ef19ec2e4f483901d202e90150580b6123c3d492e26e7965f488c")]
    [InlineData("native", "output-value", "aad2b61bd2405dfcf7294fc2be85f325694f02dda22d0af30381cb50d8295e0a")]
    [InlineData("native", "sequence", "0818bd0a3703638d4f01014c92cf866a8903cab36df2fa2506dc0d06b94295e8")]
    [InlineData("native", "payload", "72ea6c2871e0f44499f1c2b556f265d9424bfea67cca9cb343b4b040ead65525")]
    [InlineData("subnetwork", "none", "b2f421c933eb7e1a91f1d9e1efa3f120fe419326c0dbac487752189522550e0c")]
    [InlineData("subnetwork", "payload", "12ab63b9aea3d58db339245a9b6e9cb6075b2253615ce0fb18104d28de4435a1")]
    [InlineData("subnetwork", "gas", "2501edfc0068d591160c4bd98646c6e6892cdc051182a8be3ccd6d67f104fd17")]
    [InlineData("subnetwork", "subnetwork-id", "a5d1230ede0dfcfd522e04123a7bcd721462fed1d3a87352031a4f6e3c4389b6")]
    public void Official_signature_hash(string kind, string mutation, string expectedHex)
    {
        var tx = Build(kind == "subnetwork", mutation);
        var hash = SigHash.CalcSchnorrSignatureHash(tx, inputIndex: 0, UtxoEntries()[0]);
        Assert.Equal(expectedHex, Convert.ToHexString(hash).ToLowerInvariant());
    }

    private static Transaction Build(bool subnetwork, string mutation)
    {
        var prev = Convert.FromHexString(PrevTxIdHex);
        var index1 = mutation == "outpoint-index" ? 2u : 1u;
        var sequence1 = mutation == "sequence" ? 12345UL : 1UL;
        var output1 = mutation == "output-value" ? 100UL : 300UL;

        byte[] subnetworkId;
        ulong gas;
        byte[] payload;
        if (!subnetwork)
        {
            subnetworkId = new byte[20];
            gas = 0;
            payload = mutation == "payload" ? Payload666() : Array.Empty<byte>();
        }
        else
        {
            subnetworkId = mutation == "subnetwork-id" ? Pad20(Payload666()) : Pad20(BytesFromTo(1, 10));
            gas = mutation == "gas" ? 1234UL : 250UL;
            payload = mutation == "payload" ? Payload666() : BytesFromTo(10, 20);
        }

        var inputs = new[]
        {
            new TransactionInput(new TransactionOutpoint(prev, 0), Array.Empty<byte>(), sequence: 0, sigOpCount: 0),
            new TransactionInput(new TransactionOutpoint(prev, index1), Array.Empty<byte>(), sequence1, sigOpCount: 0),
            new TransactionInput(new TransactionOutpoint(prev, 2), Array.Empty<byte>(), sequence: 2, sigOpCount: 0),
        };
        var outputs = new[]
        {
            new TransactionOutput(300, Spk2()),
            new TransactionOutput(output1, Spk1()),
        };
        return new Transaction(version: 0, inputs, outputs, lockTime: 1615462089000, subnetworkId, gas, payload);
    }

    private static KasWallet.Core.Consensus.UtxoEntry[] UtxoEntries() =>
    [
        new KasWallet.Core.Consensus.UtxoEntry(100, Spk1(), blockDaaScore: 0, isCoinbase: false),
        new KasWallet.Core.Consensus.UtxoEntry(200, Spk2(), blockDaaScore: 0, isCoinbase: false),
        new KasWallet.Core.Consensus.UtxoEntry(300, Spk2(), blockDaaScore: 0, isCoinbase: false),
    ];

    private static ScriptPublicKey Spk1() => new(0, Convert.FromHexString(Spk1Hex));

    private static ScriptPublicKey Spk2() => new(0, Convert.FromHexString(Spk2Hex));

    private static byte[] Payload666() => [6, 6, 6, 4, 2, 0, 1, 3, 3, 7];

    /// <summary>Inclusive byte range, so 10..20 is the 11-byte payload from the vector.</summary>
    private static byte[] BytesFromTo(int first, int last)
    {
        var bytes = new byte[last - first + 1];
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] = (byte)(first + i);
        return bytes;
    }

    private static byte[] Pad20(byte[] prefix)
    {
        var id = new byte[20];
        prefix.CopyTo(id, 0);
        return id;
    }
}
