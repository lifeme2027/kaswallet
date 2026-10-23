using System.Text.Json;
using KasWallet;
using Xunit;

namespace KasWallet.Core.Tests;

public class TransactionBuilderTests
{
    private const string TestnetZero = "kaspatest:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqhqrxplya";
    private const string Mnemonic =
        "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

    [Fact]
    public void Builds_unsigned_plan_with_change()
    {
        var utxos = new[]
        {
            new UtxoEntry(TestnetZero, "aa", 0, 5_0000_0000, false, 1)
        };
        var plan = TransactionBuilder.BuildPayment(
            KaspaNetwork.Testnet10,
            utxos,
            TestnetZero,
            1_0000_0000,
            TestnetZero,
            10_000);
        Assert.Single(plan.SelectedInputs);
        Assert.True(plan.ChangeSompi > 0);
        Assert.Contains("Unsigned", plan.Notes);
    }

    [Fact]
    public void Rejects_bad_checksum_destination()
    {
        var utxos = new[] { new UtxoEntry(TestnetZero, "aa", 0, 5_0000_0000, false, 1) };
        Assert.Throws<ArgumentException>(() => TransactionBuilder.BuildPayment(
            KaspaNetwork.Testnet10,
            utxos,
            "kaspatest:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4e",
            1_0000_0000,
            TestnetZero,
            10_000));
    }

    [Fact]
    public void Draft_from_mnemonic_attaches_derived_change_and_placeholders()
    {
        var utxos = new[] { new UtxoEntry(TestnetZero, "aa", 0, 5_0000_0000, false, 1) };
        var draft = TransactionBuilder.BuildDraftFromMnemonic(
            Mnemonic,
            changeIndex: 0,
            KaspaNetwork.Testnet10,
            utxos,
            TestnetZero,
            1_0000_0000,
            passphrase: null,
            feeSompi: 10_000);

        Assert.False(draft.HasSchnorrSignatures);
        Assert.Null(draft.Inputs[0].SchnorrSignatureHex);
        Assert.Null(draft.Inputs[0].SignatureScriptHex);
        Assert.Equal(2, draft.Outputs.Count);
        Assert.Equal(KeyDerivation.ChangePath(0), draft.ChangeDerivationPath);
        Assert.NotNull(draft.ChangeAddress);
        Assert.True(AddressValidator.IsValid(draft.ChangeAddress));
        Assert.StartsWith("kaspatest:", draft.ChangeAddress);
        Assert.NotEqual(TestnetZero, draft.ChangeAddress);
        Assert.Contains("placeholder", draft.Notes, StringComparison.OrdinalIgnoreCase);

        var json = JsonSerializer.Serialize(draft);
        Assert.Contains("ChangeAddress", json);
        Assert.Contains("\"SchnorrSignatureHex\":null", json);

        var submit = KaspaRestClient.BuildSubmitJson(draft);
        using var doc = JsonDocument.Parse(submit);
        Assert.Equal("", doc.RootElement.GetProperty("transaction").GetProperty("inputs")[0].GetProperty("signatureScript").GetString());
        var script = doc.RootElement.GetProperty("transaction").GetProperty("outputs")[0].GetProperty("scriptPublicKey").GetProperty("scriptPublicKey").GetString();
        Assert.Equal("20" + new string('0', 64) + "ac", script);
        Assert.Equal(KaspaRestClient.NativeSubnetworkId, doc.RootElement.GetProperty("transaction").GetProperty("subnetworkId").GetString());
    }
}
