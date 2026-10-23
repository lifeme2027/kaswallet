using NBitcoin.Secp256k1;

namespace KasWallet;

/// <summary>
/// Schnorr P2PK addresses: version byte 0 plus the 32-byte x-only pubkey.
/// </summary>
public static class SchnorrAddress
{
    public static string FromXOnlyPublicKey(ReadOnlySpan<byte> xOnlyPubKey, KaspaNetwork network)
    {
        if (xOnlyPubKey.Length != 32)
            throw new ArgumentException("Schnorr x-only public key must be 32 bytes.", nameof(xOnlyPubKey));
        if (!ECXOnlyPubKey.TryCreate(xOnlyPubKey, out _))
            throw new ArgumentException("x-only public key is not on the secp256k1 curve.", nameof(xOnlyPubKey));

        return KaspaBech32.Encode(KaspaNetworks.AddressPrefix(network), KaspaBech32.PubKeyVersion, xOnlyPubKey);
    }

    public static string GenerateReceiveAddress(string mnemonic, int index, KaspaNetwork network, string? passphrase = null)
    {
        var xOnly = KeyDerivation.DeriveXOnlyPublicKey(mnemonic, index, change: false, passphrase);
        return FromXOnlyPublicKey(xOnly, network);
    }

    public static string GenerateChangeAddress(string mnemonic, int index, KaspaNetwork network, string? passphrase = null)
    {
        var xOnly = KeyDerivation.DeriveXOnlyPublicKey(mnemonic, index, change: true, passphrase);
        return FromXOnlyPublicKey(xOnly, network);
    }

    public static string ScriptForAddress(string address)
    {
        if (!AddressValidator.TryDecode(address, out var info))
            throw new ArgumentException("Invalid Kaspa address.", nameof(address));

        var payload = Convert.ToHexString(info.Payload).ToLowerInvariant();
        return info.Version switch
        {
            KaspaBech32.PubKeyVersion => "20" + payload + "ac",
            KaspaBech32.PubKeyEcdsaVersion => "21" + payload + "ab",
            KaspaBech32.ScriptHashVersion => "aa20" + payload + "87",
            _ => throw new ArgumentException("Unsupported address version.", nameof(address))
        };
    }
}
