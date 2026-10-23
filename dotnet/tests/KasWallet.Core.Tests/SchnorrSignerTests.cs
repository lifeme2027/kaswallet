using KasWallet;
using Xunit;

namespace KasWallet.Core.Tests;

public class SchnorrSignerTests
{
    private const string Mnemonic =
        "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

    [Fact]
    public void Message_sign_verify_roundtrip_matches_receive_address()
    {
        var address = SchnorrAddress.GenerateReceiveAddress(Mnemonic, 2, KaspaNetwork.Mainnet);
        Assert.True(AddressValidator.TryDecode(address, out var info));
        var message = "kaswallet schnorr"u8.ToArray();
        var signature = SchnorrSigner.SignMessageWithMnemonic(Mnemonic, 2, message);
        Assert.Equal(64, signature.Length);
        Assert.True(SchnorrSigner.VerifyMessage(info.Payload, message, signature));

        signature[0] ^= 0x01;
        Assert.False(SchnorrSigner.VerifyMessage(info.Payload, message, signature));
    }

    [Fact]
    public void Private_key_one_signs_the_known_x_only_key()
    {
        var secret = new byte[32];
        secret[31] = 1;
        var xOnly = Convert.FromHexString("79be667ef9dcbbac55a06295ce870b07029bfcdb2dce28d959f2815b16f81798");
        var digest = SchnorrSigner.HashMessage("bip340"u8);
        var signature = SchnorrSigner.SignDigest(secret, digest);
        Assert.True(SchnorrSigner.VerifyDigest(xOnly, digest, signature));
        Assert.False(SchnorrSigner.VerifyDigest(xOnly, SchnorrSigner.HashMessage("other"u8), signature));
    }

    [Fact]
    public void Index_zero_signature_does_not_verify_for_index_one()
    {
        var message = new byte[] { 1, 2, 3, 4 };
        var signature = SchnorrSigner.SignMessageWithMnemonic(Mnemonic, 0, message);
        var other = SchnorrAddress.GenerateReceiveAddress(Mnemonic, 1, KaspaNetwork.Mainnet);
        Assert.True(AddressValidator.TryDecode(other, out var info));
        Assert.False(SchnorrSigner.VerifyMessage(info.Payload, message, signature));
    }
}
