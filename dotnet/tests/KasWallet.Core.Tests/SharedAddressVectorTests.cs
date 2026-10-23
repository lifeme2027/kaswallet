using KasWallet;
using Xunit;

namespace KasWallet.Core.Tests;

/// <summary>
/// Same valid and invalid address strings as
/// <c>Kaspadotnet.Clients.Tests.SharedAddressVectors</c>.
/// Drop-in for the kaswallet test project. Not compiled from that repo.
/// </summary>
public class SharedAddressVectorTests
{
    public static readonly string[] Valid =
    {
        // Schnorr, 32 zero bytes, mainnet and testnet.
        "kaspa:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4e",
        "kaspatest:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqhqrxplya",
        // ECDSA version 1, 33 zero bytes.
        "kaspatest:qyqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqhe837j2d",
        // x-only 5fff3c4d... and dff1d77f...
        "kaspa:qp0l70zd5x85ttwd6jv7g3s3a8llzj96d8dncn4zmhv4tlzx5k2jyqh70xmfj",
        "kaspa:qr0lr4ml9fn3chekrqmjdkergxl93l4wrk3dankcgvjq776s9wn9jkdskewva",
        // ECDSA compressed pubkey 02ba01fc... on testnet.
        "kaspatest:qypt5q0uta8fmxretxwxng76lkp45uj7tuhfxn5nytkd8tcep2c0vrsxarh9t02",
        // Version 0, first payload byte 0x11, on mainnet, devnet, and simnet.
        "kaspa:qqgsqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq2xdnz6yk",
        "kaspadev:qqgsqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqxcg4s836",
        "kaspasim:qqgsqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq9ksj7y69",
        // P2SH version 8, 32 zero bytes.
        "kaspa:pqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqlmtfk4dg",
    };

    public static readonly string[] Invalid =
    {
        "kaspa:qz",
        "not-an-address",
        "kaspa:",
        "btc:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq",
        // Last checksum character flipped.
        "kaspa:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4l",
        // Mainnet zero-pubkey payload under the testnet prefix.
        "kaspatest:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4e",
        // Mainnet 0x11 payload under the testnet prefix.
        "kaspatest:qqgsqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq2xdnz6yk",
        // Version 0 and 31 payload bytes: checksum is valid, length is not.
        "kaspa:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqcq84r7qz",
        // Lowercase-only. This is the mainnet zero address in upper case.
        "KASPA:QQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQKX9AWP4E",
    };

    public static IEnumerable<object[]> ValidData()
    {
        foreach (var address in Valid)
        {
            yield return new object[] { address };
        }
    }

    public static IEnumerable<object[]> InvalidData()
    {
        foreach (var address in Invalid)
        {
            yield return new object[] { address };
        }
    }

    [Theory]
    [MemberData(nameof(ValidData))]
    public void Accepts_shared_vector(string address) =>
        Assert.True(AddressValidator.IsValid(address));

    [Theory]
    [MemberData(nameof(InvalidData))]
    public void Rejects_shared_vector(string address) =>
        Assert.False(AddressValidator.IsValid(address));
}
