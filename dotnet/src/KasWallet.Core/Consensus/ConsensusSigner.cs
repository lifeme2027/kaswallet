using NBitcoin.Secp256k1;

namespace KasWallet.Core.Consensus;

/// <summary>
/// Signs consensus inputs with BIP-340 over <see cref="SigHash"/>.
/// Signature script is OP_DATA_65 || 64-byte Schnorr sig || SigHashAll.
/// </summary>
public static class ConsensusSigner
{
    public static void SignInput(Transaction tx, int inputIndex, IReadOnlyList<UtxoEntry> utxoEntries, ReadOnlySpan<byte> privateKey32)
    {
        if (tx is null)
            throw new ArgumentNullException(nameof(tx));
        if (utxoEntries is null)
            throw new ArgumentNullException(nameof(utxoEntries));
        if (privateKey32.Length != 32)
            throw new ArgumentException("Private key must be 32 bytes.", nameof(privateKey32));
        if ((uint)inputIndex >= (uint)tx.Inputs.Count)
            throw new ArgumentOutOfRangeException(nameof(inputIndex));
        if ((uint)inputIndex >= (uint)utxoEntries.Count)
            throw new ArgumentException("Missing UTXO entry for the input.", nameof(utxoEntries));

        var hash = SigHash.CalcSchnorrSignatureHash(tx, inputIndex, utxoEntries[inputIndex]);
        if (!ECPrivKey.TryCreate(privateKey32, out var priv) || priv is null)
            throw new ArgumentException("Invalid secp256k1 private key.", nameof(privateKey32));

        using (priv)
        {
            var signature = priv.SignBIP340(hash).ToBytes();
            if (signature.Length != 64)
                throw new InvalidOperationException("BIP-340 signature was not 64 bytes.");

            var script = new byte[66];
            script[0] = 0x41;
            signature.CopyTo(script.AsSpan(1));
            script[65] = SigHash.SigHashAll;
            tx.Inputs[inputIndex].SignatureScript = script;
        }
    }

    public static void SignAll(Transaction tx, IReadOnlyList<UtxoEntry> utxoEntries, ReadOnlySpan<byte> privateKey32)
    {
        if (tx is null)
            throw new ArgumentNullException(nameof(tx));
        for (var i = 0; i < tx.Inputs.Count; i++)
            SignInput(tx, i, utxoEntries, privateKey32);
    }

    /// <summary>P2PK script: OP_DATA_32 || x-only key || OP_CHECKSIG.</summary>
    public static byte[] PayToPubKeyScript(ReadOnlySpan<byte> xOnlyPublicKey)
    {
        if (xOnlyPublicKey.Length != 32)
            throw new ArgumentException("x-only public key must be 32 bytes.", nameof(xOnlyPublicKey));

        var script = new byte[34];
        script[0] = 0x20;
        xOnlyPublicKey.CopyTo(script.AsSpan(1));
        script[33] = 0xac;
        return script;
    }
}
