using KasWallet;
using Xunit;

namespace KasWallet.Core.Tests;

public class MnemonicServiceTests
{
    [Fact]
    public void Generate_and_validate_12_words()
    {
        var m = MnemonicService.Generate(12);
        Assert.Equal(12, m.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.True(MnemonicService.IsValid(m));
        Assert.False(string.IsNullOrWhiteSpace(MnemonicService.GetAccountFingerprintHex(m)));
    }

    [Fact]
    public void Rejects_garbage_mnemonic()
        => Assert.False(MnemonicService.IsValid("not a real bip39 phrase at all ever"));
}
