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
}
