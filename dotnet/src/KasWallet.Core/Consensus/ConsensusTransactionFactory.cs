using KasWallet;

namespace KasWallet.Core.Consensus;

/// <summary>
/// Builds and signs a version-0 native payment from REST UTXOs.
/// </summary>
public static class ConsensusTransactionFactory
{
    public static Transaction Build(
        IReadOnlyList<KasWallet.UtxoEntry> availableUtxos,
        string destination,
        long amountSompi,
        long feeSompi,
        string? changeAddress,
        ReadOnlySpan<byte> privateKey)
    {
        if (availableUtxos is null)
            throw new ArgumentNullException(nameof(availableUtxos));
        if (destination is null)
            throw new ArgumentNullException(nameof(destination));
        if (privateKey.Length != 32)
            throw new ArgumentException("Private key must be 32 bytes.", nameof(privateKey));
        if (amountSompi <= 0)
            throw new ArgumentOutOfRangeException(nameof(amountSompi));
        if (feeSompi < 0)
            throw new ArgumentOutOfRangeException(nameof(feeSompi));

        var need = checked(amountSompi + feeSompi);
        var selected = new List<KasWallet.UtxoEntry>();
        long total = 0;
        foreach (var utxo in availableUtxos.OrderByDescending(u => u.AmountSompi))
        {
            if (utxo is null)
                throw new ArgumentException("UTXO entry is null.", nameof(availableUtxos));
            if (utxo.AmountSompi < 0)
                throw new ArgumentOutOfRangeException(nameof(availableUtxos), "UTXO amount must be >= 0.");

            selected.Add(utxo);
            total = checked(total + utxo.AmountSompi);
            if (total >= need)
                break;
        }

        if (total < need)
            throw new InvalidOperationException($"Insufficient funds: have {total} sompi, need {need}.");

        var change = total - need;
        var outputs = new List<TransactionOutput>
        {
            new((ulong)amountSompi, ScriptFor(destination))
        };
        if (change > 0)
        {
            if (string.IsNullOrWhiteSpace(changeAddress))
                throw new InvalidOperationException("Change is required but changeAddress was not provided.");
            outputs.Add(new TransactionOutput((ulong)change, ScriptFor(changeAddress)));
        }

        var inputs = new List<TransactionInput>(selected.Count);
        var spent = new List<UtxoEntry>(selected.Count);
        foreach (var utxo in selected)
        {
            var script = ScriptFor(utxo.Address);
            inputs.Add(new TransactionInput(
                new TransactionOutpoint(TransactionJson.ParseTransactionId(utxo.TransactionId), utxo.Index),
                signatureScript: Array.Empty<byte>(),
                sequence: 0,
                sigOpCount: 1));
            spent.Add(new UtxoEntry((ulong)utxo.AmountSompi, script, utxo.BlockDaaScore ?? 0, utxo.IsCoinbase));
        }

        var tx = new Transaction(
            version: 0,
            inputs,
            outputs,
            lockTime: 0,
            subnetworkId: new byte[20],
            gas: 0,
            payload: Array.Empty<byte>());
        ConsensusSigner.SignAll(tx, spent, privateKey);
        return tx;
    }

    /// <summary>Hex private key (32 bytes), or a BIP39 mnemonic at receive index 0.</summary>
    public static byte[] SigningKeyFrom(string fromKey, string? passphrase = null)
    {
        if (string.IsNullOrWhiteSpace(fromKey))
            throw new ArgumentException("A hex private key or BIP39 mnemonic is required.", nameof(fromKey));

        var text = fromKey.Trim();
        if (!text.Contains(' ') && !text.Contains('\t') && !text.Contains('\n'))
        {
            var hex = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text[2..] : text;
            if (hex.Length == 64 && IsHex(hex))
                return Convert.FromHexString(hex);
            if (hex.Length > 0 && IsHex(hex))
                throw new ArgumentException("Private key hex must be 32 bytes (64 hex characters).", nameof(fromKey));
        }

        return KeyDerivation.DeriveSecret(text, index: 0, change: false, passphrase);
    }

    public static string AddressFromPrivateKey(ReadOnlySpan<byte> privateKey, KaspaNetwork network)
    {
        var xOnly = KeyDerivation.XOnlyFromSecret(privateKey);
        return SchnorrAddress.FromXOnlyPublicKey(xOnly, network);
    }

    private static ScriptPublicKey ScriptFor(string address)
    {
        if (!AddressValidator.TryDecode(address, out var info))
            throw new ArgumentException("Invalid Kaspa address.", nameof(address));

        // Schnorr P2PK is version byte 0 and a 32-byte x-only key.
        var script = info.Version == KaspaBech32.PubKeyVersion
            ? ConsensusSigner.PayToPubKeyScript(info.Payload)
            : Convert.FromHexString(SchnorrAddress.ScriptForAddress(address));
        return new ScriptPublicKey(0, script);
    }

    private static bool IsHex(string value)
    {
        foreach (var c in value)
        {
            if (!Uri.IsHexDigit(c))
                return false;
        }

        return value.Length > 0;
    }
}
