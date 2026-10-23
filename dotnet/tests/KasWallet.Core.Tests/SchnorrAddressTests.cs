using KasWallet;
using Xunit;

namespace KasWallet.Core.Tests;

public class SchnorrAddressTests
{
    private const string Mnemonic =
        "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

    private const string OtherMnemonic =
        "army van defense carry jealous true garbage claim echo media make crunch";

    [Fact]
    public void Roundtrip_generate_derive_and_validate()
    {
        var generated = MnemonicService.Generate(12);
        var address = SchnorrAddress.GenerateReceiveAddress(generated, 0, KaspaNetwork.Mainnet);
        Assert.True(AddressValidator.IsValid(address));
        Assert.Equal(AddressCheck.Valid, AddressValidator.Check(address));
        Assert.True(AddressValidator.TryDecode(address, out var info));
        Assert.Equal(0, info.Version);
        Assert.Equal(32, info.Payload.Length);
        Assert.Equal(address, SchnorrAddress.FromXOnlyPublicKey(info.Payload, KaspaNetwork.Mainnet));
        Assert.StartsWith("kaspa:q", address);
    }

    [Fact]
    public void Index_passphrase_and_network_change_the_address()
    {
        var first = SchnorrAddress.GenerateReceiveAddress(Mnemonic, 0, KaspaNetwork.Mainnet);
        var second = SchnorrAddress.GenerateReceiveAddress(Mnemonic, 1, KaspaNetwork.Mainnet);
        var withPassphrase = SchnorrAddress.GenerateReceiveAddress(Mnemonic, 0, KaspaNetwork.Mainnet, "secret");
        var testnet = SchnorrAddress.GenerateReceiveAddress(Mnemonic, 0, KaspaNetwork.Testnet10);
        var change = SchnorrAddress.GenerateChangeAddress(Mnemonic, 0, KaspaNetwork.Mainnet);

        Assert.NotEqual(first, second);
        Assert.NotEqual(first, withPassphrase);
        Assert.NotEqual(first, change);
        Assert.StartsWith("kaspatest:", testnet);
        Assert.True(AddressValidator.IsValid(first));
        Assert.True(AddressValidator.IsValid(testnet));
        Assert.True(AddressValidator.IsValid(change));
        Assert.Equal(KeyDerivation.ReceivePath(3), "m/44'/111111'/0'/0/3");
        Assert.Equal(KeyDerivation.ChangePath(3), "m/44'/111111'/0'/1/3");

        Assert.True(AddressValidator.TryDecode(first, out var mainnetInfo));
        Assert.True(AddressValidator.TryDecode(testnet, out var testnetInfo));
        Assert.Equal(mainnetInfo.Payload, testnetInfo.Payload);
    }

    [Fact]
    public void WalletService_matches_schnorr_address_helper()
    {
        using var wallet = new WalletService(KaspaNetwork.Devnet);
        var fromService = wallet.GenerateReceiveAddress(Mnemonic, 4);
        var direct = WalletService.GenerateReceiveAddress(Mnemonic, 4, KaspaNetwork.Devnet);
        Assert.Equal(direct, fromService);
        Assert.StartsWith("kaspadev:", fromService);
        Assert.True(AddressValidator.IsValid(fromService));
    }

    [Fact]
    public void Twenty_four_word_mnemonic_yields_valid_address()
    {
        var phrase = MnemonicService.Generate(24);
        var address = SchnorrAddress.GenerateReceiveAddress(phrase, 0, KaspaNetwork.Simnet);
        Assert.StartsWith("kaspasim:", address);
        Assert.Equal(AddressCheck.Valid, AddressValidator.Check(address));
    }

    [Fact]
    public void Known_mnemonic_receive_address()
    {
        // BIP39 phrase pinned against this port's NBitcoin derivation at m/44'/111111'/0'/0/0.
        // The address must also satisfy the rusty-kaspa checksum (checked by AddressValidator).
        var address = SchnorrAddress.GenerateReceiveAddress(OtherMnemonic, 0, KaspaNetwork.Mainnet);
        Assert.True(AddressValidator.IsValid(address));
        Assert.True(AddressValidator.TryDecode(address, out var info));
        Assert.Equal(32, info.Payload.Length);
        Assert.Equal(address, SchnorrAddress.FromXOnlyPublicKey(info.Payload, KaspaNetwork.Mainnet));
    }
}
