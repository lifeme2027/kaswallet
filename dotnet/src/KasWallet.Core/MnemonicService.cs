using NBitcoin;

namespace KasWallet;

/// <summary>
/// BIP39 mnemonics. Kaspa commonly uses BIP44 coin type 111111 (documented; address encoding TBD).
/// </summary>
public static class MnemonicService
{
    public const int KaspaCoinType = 111111;
    public const string DefaultDerivationPath = "m/44'/111111'/0'";

    public static string Generate(int wordCount = 12)
    {
        var count = wordCount switch
        {
            12 => WordCount.Twelve,
            24 => WordCount.TwentyFour,
            _ => throw new ArgumentOutOfRangeException(nameof(wordCount), "Use 12 or 24 words.")
        };
        return new Mnemonic(Wordlist.English, count).ToString();
    }

    public static bool IsValid(string mnemonic)
    {
        try
        {
            _ = new Mnemonic(mnemonic, Wordlist.English);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static ExtKey DeriveAccountRoot(string mnemonic, string? passphrase = null)
    {
        var m = new Mnemonic(mnemonic, Wordlist.English);
        var root = m.DeriveExtKey(passphrase);
        // m/44'/111111'/0'
        return root.Derive(new KeyPath($"44'/{KaspaCoinType}'/0'"));
    }

    public static string GetAccountFingerprintHex(string mnemonic, string? passphrase = null)
    {
        var account = DeriveAccountRoot(mnemonic, passphrase);
        return account.Neuter().PubKey.Hash.ToString();
    }
}
