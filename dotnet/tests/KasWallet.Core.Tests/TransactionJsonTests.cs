using System.Text.Json;
using KasWallet.Core.Consensus;
using Xunit;

namespace KasWallet.Core.Tests;

public class TransactionJsonTests
{
    [Fact]
    public void Rest_and_wrpc_json_roundtrip_through_json_document()
    {
        var txId = new byte[32];
        txId[0] = 0x11;
        txId[31] = 0x22;
        var signature = new byte[] { 0x41, 0xaa, 0x01 };
        var script = new byte[] { 0x20, 0xbb, 0xac };
        var subnetwork = new byte[20];
        subnetwork[19] = 0x01;
        var tx = new Transaction(
            version: 0,
            inputs: new[]
            {
                new TransactionInput(new TransactionOutpoint(txId, 3), signature, sequence: 9, sigOpCount: 1)
            },
            outputs: new[]
            {
                new TransactionOutput(123, new ScriptPublicKey(0, script))
            },
            lockTime: 7,
            subnetwork,
            gas: 99,
            payload: new byte[] { 0xab });

        var rest = TransactionJson.ToRestSubmitJson(tx);
        using (var doc = JsonDocument.Parse(rest))
        {
            var root = doc.RootElement;
            Assert.False(root.GetProperty("allowOrphan").GetBoolean());
            var body = root.GetProperty("transaction");
            Assert.False(body.TryGetProperty("gas", out _));
            Assert.False(body.TryGetProperty("payload", out _));
            AssertShape(body, txId, signature, script, subnetwork);
        }

        var wrpc = TransactionJson.ToWrpcSubmitTransactionJson(tx, allowOrphan: true);
        using (var doc = JsonDocument.Parse(wrpc))
        {
            var root = doc.RootElement;
            Assert.True(root.GetProperty("allowOrphan").GetBoolean());
            var body = root.GetProperty("transaction");
            Assert.Equal(JsonValueKind.Number, body.GetProperty("gas").ValueKind);
            Assert.Equal(99UL, body.GetProperty("gas").GetUInt64());
            Assert.Equal("ab", body.GetProperty("payload").GetString());
            AssertShape(body, txId, signature, script, subnetwork);
        }

        Assert.Contains("\"allowOrphan\":true", TransactionJson.ToRestSubmitJson(tx, allowOrphan: true));

        var native = new Transaction(0, tx.Inputs, tx.Outputs, lockTime: 0, new byte[20], gas: 0, payload: Array.Empty<byte>());
        using var nativeDoc = JsonDocument.Parse(TransactionJson.ToWrpcSubmitTransactionJson(native));
        var nativeBody = nativeDoc.RootElement.GetProperty("transaction");
        Assert.Equal(JsonValueKind.Number, nativeBody.GetProperty("gas").ValueKind);
        Assert.Equal(0UL, nativeBody.GetProperty("gas").GetUInt64());
        Assert.Equal("", nativeBody.GetProperty("payload").GetString());
        Assert.Equal("0000000000000000000000000000000000000000", nativeBody.GetProperty("subnetworkId").GetString());
        Assert.False(nativeDoc.RootElement.GetProperty("allowOrphan").GetBoolean());
    }

    [Fact]
    public void Transaction_id_hex_roundtrips_as_raw_bytes_unreversed()
    {
        var txId = new byte[32];
        txId[0] = 0x11;
        txId[31] = 0x22;
        var hex = Convert.ToHexString(txId).ToLowerInvariant();

        Assert.Equal("11" + new string('0', 60) + "22", hex);
        Assert.Equal(hex, TransactionJson.FormatTransactionId(txId));
        Assert.Equal(txId, TransactionJson.ParseTransactionId(hex));
        Assert.Equal(hex, TransactionJson.FormatTransactionId(TransactionJson.ParseTransactionId(hex)));
    }

    private static void AssertShape(JsonElement body, byte[] txId, byte[] signature, byte[] script, byte[] subnetwork)
    {
        Assert.Equal(JsonValueKind.Number, body.GetProperty("version").ValueKind);
        Assert.Equal(0, body.GetProperty("version").GetInt32());
        Assert.Equal(JsonValueKind.Number, body.GetProperty("lockTime").ValueKind);
        Assert.Equal(7UL, body.GetProperty("lockTime").GetUInt64());
        Assert.Equal(Convert.ToHexString(subnetwork).ToLowerInvariant(), body.GetProperty("subnetworkId").GetString());

        var input = body.GetProperty("inputs")[0];
        var outpoint = input.GetProperty("previousOutpoint");
        var transactionId = outpoint.GetProperty("transactionId").GetString();
        Assert.NotNull(transactionId);
        Assert.Matches("^[0-9a-f]{64}$", transactionId);
        Assert.Equal(txId, TransactionJson.ParseTransactionId(transactionId));
        Assert.Equal(TransactionJson.FormatTransactionId(txId), transactionId);
        Assert.Equal(Convert.ToHexString(txId).ToLowerInvariant(), transactionId);
        Assert.Equal("11" + new string('0', 60) + "22", transactionId);
        Assert.Equal(JsonValueKind.Number, outpoint.GetProperty("index").ValueKind);
        Assert.Equal(3u, outpoint.GetProperty("index").GetUInt32());

        var signatureHex = input.GetProperty("signatureScript").GetString();
        Assert.Equal(Convert.ToHexString(signature).ToLowerInvariant(), signatureHex);
        Assert.Equal(signature, Convert.FromHexString(signatureHex!));
        Assert.Equal(JsonValueKind.Number, input.GetProperty("sequence").ValueKind);
        Assert.Equal(9UL, input.GetProperty("sequence").GetUInt64());
        Assert.Equal(JsonValueKind.Number, input.GetProperty("sigOpCount").ValueKind);
        Assert.Equal(1, input.GetProperty("sigOpCount").GetInt32());

        var output = body.GetProperty("outputs")[0];
        Assert.Equal(JsonValueKind.Number, output.GetProperty("amount").ValueKind);
        Assert.Equal(123UL, output.GetProperty("amount").GetUInt64());
        var spk = output.GetProperty("scriptPublicKey");
        Assert.Equal(0, spk.GetProperty("version").GetInt32());
        var scriptHex = spk.GetProperty("scriptPublicKey").GetString();
        Assert.Equal("20bbac", scriptHex);
        Assert.Equal(script, Convert.FromHexString(scriptHex!));
    }
}
