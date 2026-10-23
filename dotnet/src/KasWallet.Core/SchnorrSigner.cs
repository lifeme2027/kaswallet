using System.Security.Cryptography;
using NBitcoin.Secp256k1;

namespace KasWallet;

/// <summary>
/// BIP-340 Schnorr message signatures via NBitcoin.Secp256k1.
/// This is not Kaspa consensus sighash. Transaction inputs stay unsigned
/// until a blake2b sighash over the transaction is implemented.
/// </summary>
public static class SchnorrSigner
{
    public static byte[] HashMessage(ReadOnlySpan<byte> message) => System.Security.Cryptography.SHA256.HashData(message);

    public static byte[] SignDigest(ReadOnlySpan<byte> privateKey32, ReadOnlySpan<byte> digest32)
    {
        if (privateKey32.Length != 32)
            throw new ArgumentException("Private key must be 32 bytes.", nameof(privateKey32));
        if (digest32.Length != 32)
            throw new ArgumentException("Digest must be 32 bytes.", nameof(digest32));

        var secret = privateKey32.ToArray();
        try
        {
            using var priv = ECPrivKey.Create(secret);
            var signature = priv.SignBIP340(digest32).ToBytes();
            if (signature.Length != 64)
                throw new InvalidOperationException("BIP-340 signature was not 64 bytes.");
            return signature;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    public static byte[] SignMessage(ReadOnlySpan<byte> message, ReadOnlySpan<byte> privateKey32)
        => SignDigest(privateKey32, HashMessage(message));

    public static byte[] SignDigestWithMnemonic(
        string mnemonic,
        int index,
        ReadOnlySpan<byte> digest32,
        string? passphrase = null,
        bool change = false)
    {
        var secret = KeyDerivation.DeriveSecret(mnemonic, index, change, passphrase);
        try
        {
            return SignDigest(secret, digest32);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    public static byte[] SignMessageWithMnemonic(
        string mnemonic,
        int index,
        ReadOnlySpan<byte> message,
        string? passphrase = null,
        bool change = false)
        => SignDigestWithMnemonic(mnemonic, index, HashMessage(message), passphrase, change);

    public static bool VerifyDigest(ReadOnlySpan<byte> xOnlyPubKey32, ReadOnlySpan<byte> digest32, ReadOnlySpan<byte> signature64)
    {
        if (xOnlyPubKey32.Length != 32 || digest32.Length != 32 || signature64.Length != 64)
            return false;
        if (!ECXOnlyPubKey.TryCreate(xOnlyPubKey32, out var pubKey))
            return false;
        if (!SecpSchnorrSignature.TryCreate(signature64, out var signature))
            return false;
        return pubKey.SigVerifyBIP340(signature, digest32);
    }

    public static bool VerifyMessage(ReadOnlySpan<byte> xOnlyPubKey32, ReadOnlySpan<byte> message, ReadOnlySpan<byte> signature64)
        => VerifyDigest(xOnlyPubKey32, HashMessage(message), signature64);
}
