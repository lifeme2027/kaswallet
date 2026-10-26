using KasWallet.Core.Consensus;
using Xunit;

namespace KasWallet.Core.Tests;

/// <summary>
/// Official vectors from rusty-kaspa consensus/core/src/hashing/tx.rs test_transaction_hashing
/// (tests #1-#9, version 0). Tests #10-#14 are version 1 (new id scheme) and are not covered.
/// Hex is raw hash bytes, same as Rust's Hash Display/FromStr.
/// </summary>
public class TransactionIdVectorTests
{
    private const string NonZeroPrev = "59b3d6dc6cdc660c389c3fdb5704c48c598d279cdf1bab54182db586a4c95dd5";

    private static byte[] Subnet(byte first)
    {
        var b = new byte[20];
        b[0] = first;
        return b;
    }

    private static TransactionInput Input(string prevHex, uint index) =>
        new(new TransactionOutpoint(Convert.FromHexString(prevHex), index), new byte[] { 1, 2 }, 7, 5);

    private static TransactionOutput Output() =>
        new(1564, new ScriptPublicKey(7, new byte[] { 1, 2, 3, 4, 5 }));

    private static readonly string ZeroPrev = new('0', 64);

    public static IEnumerable<object[]> Vectors()
    {
        // Test numbers match the Rust source.
        yield return Case(1, Array.Empty<TransactionInput>(), Array.Empty<TransactionOutput>(), 0, 0, 0, Array.Empty<byte>(), 0,
            "2c18d5e59ca8fc4c23d9560da3bf738a8f40935c11c162017fbf2c907b7e665c",
            "c9e29784564c269ce2faaffd3487cb4684383018ace11133de082dce4bb88b0b");
        yield return Case(2, new[] { Input(ZeroPrev, 2) }, Array.Empty<TransactionOutput>(), 0, 0, 0, Array.Empty<byte>(), 0,
            "b2d65ae36e123eb73f253176d7234a57656b84d0d60b9fc746ab0d0f085c9cc7",
            "7d9f7cfdd77f236a41895ac5cdda2fa42f7122964ba995fdfacebce54efad7e8");
        yield return Case(3, new[] { Input(ZeroPrev, 2) }, new[] { Output() }, 0, 0, 0, Array.Empty<byte>(), 0,
            "67289b12146d1b5ef384332137399791a5cfe89506ff31688b0d95ae821d0a0c",
            "492279c0ed5018aa00b0b2d42c1c42350285f2e689236a81829edaf818e30fdb");
        yield return Case(4, new[] { Input(ZeroPrev, 2) }, new[] { Output() }, 54, 0, 3, Array.Empty<byte>(), 0,
            "7cd34b788d7d230970d4bfd955c34c5abc49e3bcdd5adb03a77bb71d05554401",
            "de319664ee9f4197e89be0d0e08b2b6cac110efc2cf107de1fbc6bd2ce29d545");
        yield return Case(5, new[] { Input(NonZeroPrev, 2) }, new[] { Output() }, 54, 0, 3, Array.Empty<byte>(), 0,
            "c9dd78e818445f617a28348d6db752142e2fab440effa58140ad2773e638b628",
            "1be9978bcab9424f15adac6fca0a64c3f56344a7cd0ec92a225496e19a0d122c");
        // #6: valid coinbase has no inputs; subnetwork = coinbase (byte 1)
        yield return Case(6, Array.Empty<TransactionInput>(), new[] { Output() }, 54, 1, 3, Array.Empty<byte>(), 0,
            "2578783ec93c3a02414a228e10b1b5af298623254775f972f97df08d4ec28c8f",
            "dffa96c75ef9d17520991fc6d88813531e230488e75b65f65ce958f2d54d2451");
        // #7: registry subnetwork (byte 2)
        yield return Case(7, new[] { Input(NonZeroPrev, 2) }, new[] { Output() }, 54, 2, 3, Array.Empty<byte>(), 0,
            "3f6cea6d7ac8f6b2f86209fa748ea0ef5a1d5d380d43b79e77d52e770bb9a7b9",
            "9abf01c6c312dd984ff19c23bec85e8678e6ea34041fe3c5de52fd9344adac63");
        // #8: as #7 with payload; id and hash change
        yield return Case(8, new[] { Input(NonZeroPrev, 2) }, new[] { Output() }, 54, 2, 3, new byte[] { 1, 2, 3 }, 0,
            "4acda997dfb31c6518224c9ac00d0777fc7cbecdab461be3c0816b1cba19a056",
            "f0bb137ed71a91445ddf9224c76f755153a296eeb4fdc29b8393ddd81bf34ce6");
        // #9: as #8 with storage mass 5; only the hash changes
        yield return Case(9, new[] { Input(NonZeroPrev, 2) }, new[] { Output() }, 54, 2, 3, new byte[] { 1, 2, 3 }, 5,
            "4acda997dfb31c6518224c9ac00d0777fc7cbecdab461be3c0816b1cba19a056",
            "ced89bbf642cda42d29d9518d16e35cbbf85d10e1ab106b7dc2e0a821308ac91");
    }

    private static object[] Case(int n, TransactionInput[] inputs, TransactionOutput[] outputs, ulong lockTime,
        byte subnet, ulong gas, byte[] payload, ulong mass, string id, string hash)
    {
        var tx = new Transaction(0, inputs, outputs, lockTime, Subnet(subnet), gas, payload);
        return new object[] { n, tx, mass, id, hash };
    }

    [Theory]
    [MemberData(nameof(Vectors))]
    public void Official_transaction_id_and_hash(int number, Transaction tx, ulong mass, string expectedId, string expectedHash)
    {
        Assert.True(number >= 1);
        Assert.Equal(expectedId, Convert.ToHexString(SigHash.TransactionId(tx)).ToLowerInvariant());
        Assert.Equal(expectedHash, Convert.ToHexString(SigHash.TransactionHash(tx, mass)).ToLowerInvariant());
    }
}
