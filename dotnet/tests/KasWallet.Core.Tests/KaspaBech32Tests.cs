using System.Text;
using KasWallet;
using Xunit;

namespace KasWallet.Core.Tests;

public class KaspaBech32Tests
{
    [Theory]
    [InlineData("a", 0, "", "a:qqeq69uvrh")]
    [InlineData("a", 8, "", "a:pq99546ray")]
    [InlineData("b", 8, " ", "b:pqsqzsjd64fv")]
    [InlineData("b", 8, "-", "b:pqksmhczf8ud")]
    [InlineData("b", 8, "0", "b:pqcq53eqrk0e")]
    [InlineData("b", 8, "1", "b:pqcshg75y0vf")]
    [InlineData("b", 8, "-1", "b:pqknzl4e9y0zy")]
    [InlineData("b", 8, "11", "b:pqcnzt888ytdg")]
    [InlineData("b", 8, "abc", "b:ppskycc8txxxn2w")]
    [InlineData("b", 8, "1234598760", "b:pqcnyve5x5unsdekxqeusxeyu2")]
    [InlineData("b", 8, "abcdefghijklmnopqrstuvwxyz", "b:ppskycmyv4nxw6rfdf4kcmtwdac8zunnw36hvamc09aqtpppz8lk")]
    [InlineData("b", 8, "000000000000000000000000000000000000000000", "b:pqcrqvpsxqcrqvpsxqcrqvpsxqcrqvpsxqcrqvpsxqcrqvpsxqcrqvpsxqcrqvpsxqcrq7ag684l3")]
    public void Encodes_rusty_kaspa_vectors(string prefix, int version, string payloadText, string expected)
    {
        var payload = Encoding.ASCII.GetBytes(payloadText);
        Assert.Equal(expected, KaspaBech32.Encode(prefix, (byte)version, payload));

        var status = KaspaBech32.Decode(expected, out var info);
        Assert.NotEqual(KaspaBech32Status.BadChecksum, status);
        Assert.NotEqual(KaspaBech32Status.Malformed, status);
        Assert.Equal((byte)version, info.Version);
        Assert.Equal(payload, info.Payload);
    }

    [Fact]
    public void Encodes_schnorr_and_ecdsa_network_vectors()
    {
        Assert.Equal(
            "kaspa:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4e",
            KaspaBech32.Encode("kaspa", 0, new byte[32]));
        Assert.Equal(
            "kaspatest:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqhqrxplya",
            KaspaBech32.Encode("kaspatest", 0, new byte[32]));
        Assert.Equal(
            "kaspatest:qyqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqhe837j2d",
            KaspaBech32.Encode("kaspatest", 1, new byte[33]));

        var xOnly = Convert.FromHexString("5fff3c4da18f45adcdd499e44611e9fff148ba69db3c4ea2ddd955fc46a59522");
        Assert.Equal(
            "kaspa:qp0l70zd5x85ttwd6jv7g3s3a8llzj96d8dncn4zmhv4tlzx5k2jyqh70xmfj",
            KaspaBech32.Encode("kaspa", 0, xOnly));

        // ECDSA P2PK uses version 1 + 33-byte compressed pubkey
        var ecdsa = Convert.FromHexString("02ba01fc5f4e9d9879599c69a3dafd835a725e5f2e934e9322ecd3af190ab0f60e");
        var ecdsaAddr = KaspaBech32.Encode("kaspatest", 1, ecdsa);
        Assert.Equal(KaspaBech32Status.Ok, KaspaBech32.Decode(ecdsaAddr, out var ecdsaInfo));
        Assert.Equal(1, ecdsaInfo.Version);
        Assert.Equal(ecdsa, ecdsaInfo.Payload);
        Assert.True(AddressValidator.IsValid(ecdsaAddr));
    }

    [Fact]
    public void Known_compressed_pubkey_encodes_mainnet_address()
    {
        var xOnly = Convert.FromHexString("dff1d77f2a671c5f36183726db2341be58feae1da2deced843240f7b502ba659");
        var address = SchnorrAddress.FromXOnlyPublicKey(xOnly, KaspaNetwork.Mainnet);
        Assert.Equal("kaspa:qr0lr4ml9fn3chekrqmjdkergxl93l4wrk3dankcgvjq776s9wn9jkdskewva", address);
        Assert.Equal(AddressCheck.Valid, AddressValidator.Check(address));
    }

    [Fact]
    public void Prefix_changes_checksum()
    {
        var payload = new byte[32];
        payload[0] = 0x11;
        var mainnet = KaspaBech32.Encode("kaspa", 0, payload);
        var devnet = KaspaBech32.Encode("kaspadev", 0, payload);
        var simnet = KaspaBech32.Encode("kaspasim", 0, payload);
        Assert.NotEqual(mainnet, devnet);
        Assert.True(AddressValidator.IsValid(mainnet));
        Assert.True(AddressValidator.IsValid(devnet));
        Assert.True(AddressValidator.IsValid(simnet));
        Assert.Equal(AddressCheck.BadChecksum, AddressValidator.Check("kaspatest:" + mainnet.Split(':')[1]));
    }

    [Fact]
    public void Nonstandard_payload_length_is_rejected()
    {
        var address = KaspaBech32.Encode("kaspa", 0, new byte[31]);
        Assert.Equal(AddressCheck.BadPayload, AddressValidator.Check(address));
        Assert.False(AddressValidator.IsValid(address));
    }
}
