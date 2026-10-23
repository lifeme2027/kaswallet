namespace KasWallet;

public enum KaspaNetwork
{
    Mainnet,
    Testnet10,
    Devnet,
    Simnet
}

public static class KaspaNetworks
{
    public static string AddressPrefix(KaspaNetwork network) => network switch
    {
        KaspaNetwork.Mainnet => "kaspa",
        KaspaNetwork.Testnet10 => "kaspatest",
        KaspaNetwork.Devnet => "kaspadev",
        KaspaNetwork.Simnet => "kaspasim",
        _ => throw new ArgumentOutOfRangeException(nameof(network))
    };

    public static string DisplayName(KaspaNetwork network) => network switch
    {
        KaspaNetwork.Mainnet => "mainnet",
        KaspaNetwork.Testnet10 => "testnet-10",
        KaspaNetwork.Devnet => "devnet",
        KaspaNetwork.Simnet => "simnet",
        _ => network.ToString()
    };

    public static IReadOnlyList<KaspaNetwork> All { get; } =
        Enum.GetValues<KaspaNetwork>();

    public static bool IsKnownPrefix(string? prefix) => prefix is "kaspa" or "kaspatest" or "kaspadev" or "kaspasim";

    public static bool TryFromPrefix(string prefix, out KaspaNetwork network)
    {
        network = prefix switch
        {
            "kaspa" => KaspaNetwork.Mainnet,
            "kaspatest" => KaspaNetwork.Testnet10,
            "kaspadev" => KaspaNetwork.Devnet,
            "kaspasim" => KaspaNetwork.Simnet,
            _ => default
        };
        return IsKnownPrefix(prefix);
    }

    public static bool TryParse(string? name, out KaspaNetwork network)
    {
        network = default;
        if (string.IsNullOrWhiteSpace(name))
            return false;

        switch (name.Trim().ToLowerInvariant())
        {
            case "mainnet":
            case "kaspa":
                network = KaspaNetwork.Mainnet;
                return true;
            case "testnet":
            case "testnet-10":
            case "testnet10":
            case "kaspatest":
                network = KaspaNetwork.Testnet10;
                return true;
            case "devnet":
            case "kaspadev":
                network = KaspaNetwork.Devnet;
                return true;
            case "simnet":
            case "kaspasim":
                network = KaspaNetwork.Simnet;
                return true;
            default:
                return false;
        }
    }
}
