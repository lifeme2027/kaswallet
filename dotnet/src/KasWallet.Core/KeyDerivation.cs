using System.Security.Cryptography;
using NBitcoin;
using NBitcoin.Secp256k1;

namespace KasWallet;

/// <summary>
/// BIP44 receive and change keys at m/44'/111111'/0'/{0|1}/{index}.
/// Private key bytes stay inside this assembly.
/// </summary>
public static class KeyDerivation
{
    public static string ReceivePath(int index)
    {
        ValidateIndex(index);
        return $"m/44'/{MnemonicService.KaspaCoinType}'/0'/0/{index}";
    }

    public static string ChangePath(int index)
    {
        ValidateIndex(index);
        return $"m/44'/{MnemonicService.KaspaCoinType}'/0'/1/{index}";
    }

    public static byte[] DeriveXOnlyPublicKey(string mnemonic, int index, bool change = false, string? passphrase = null)
    {
        var secret = DeriveSecret(mnemonic, index, change, passphrase);
        try
        {
            return XOnlyFromSecret(secret);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    internal static byte[] DeriveSecret(string mnemonic, int index, bool change, string? passphrase)
    {
        ValidateIndex(index);
        Mnemonic parsed;
        try
        {
            parsed = new Mnemonic(mnemonic, Wordlist.English);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            throw new ArgumentException("Invalid BIP39 mnemonic.", nameof(mnemonic), ex);
        }

        var master = parsed.DeriveExtKey(passphrase ?? "");
        var child = master.Derive(new KeyPath(change ? ChangePath(index) : ReceivePath(index)));
        var secret = child.PrivateKey.ToBytes().ToArray();
        if (master is IDisposable masterDisposable)
            masterDisposable.Dispose();
        if (child is IDisposable childDisposable)
            childDisposable.Dispose();
        return secret;
    }

    internal static byte[] XOnlyFromSecret(ReadOnlySpan<byte> secret)
    {
        if (secret.Length != 32)
            throw new ArgumentException("Private key must be 32 bytes.", nameof(secret));

        var copy = secret.ToArray();
        try
        {
            using var priv = ECPrivKey.Create(copy);
            var xOnly = priv.CreateXOnlyPubKey().ToBytes();
            if (xOnly.Length != 32)
                throw new InvalidOperationException("secp256k1 x-only public key was not 32 bytes.");
            return xOnly;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(copy);
        }
    }

    private static void ValidateIndex(int index)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index), "Address index must be >= 0.");
    }
}
