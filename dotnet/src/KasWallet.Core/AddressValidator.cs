namespace KasWallet;

/// <summary>
/// Lightweight Kaspa bech32 address checks (prefix + charset + structure).
/// Full Schnorr payload decode is tracked for a later milestone.
/// </summary>
public static class AddressValidator
{
    private const string Bech32Charset = "qpzry9x8gf2tvdw0s3jn54khce6mua7l";

    public static bool IsValid(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return false;

        var parts = address.Split(':', 2);
        if (parts.Length != 2)
            return false;

        var prefix = parts[0];
        var data = parts[1];
        if (prefix is not ("kaspa" or "kaspatest" or "kaspadev" or "kaspasim"))
            return false;
        if (data.Length < 8 || data.Length > 120)
            return false;

        foreach (var c in data)
        {
            if (Bech32Charset.IndexOf(c) < 0)
                return false;
        }

        return true;
    }

    public static bool TryDetectNetwork(string address, out KaspaNetwork network)
    {
        network = default;
        if (!IsValid(address))
            return false;

        var prefix = address.Split(':', 2)[0];
        network = prefix switch
        {
            "kaspa" => KaspaNetwork.Mainnet,
            "kaspatest" => KaspaNetwork.Testnet10,
            "kaspadev" => KaspaNetwork.Devnet,
            "kaspasim" => KaspaNetwork.Simnet,
            _ => default
        };
        return true;
    }
}
