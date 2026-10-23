namespace KasWallet;

/// <summary>
/// Kaspa address checks: known prefix, bech32 charset, 40-bit checksum, and
/// standard version/payload length (Schnorr P2PK is version 0 + 32 bytes).
/// </summary>
public static class AddressValidator
{
    public static bool IsValid(string? address) => Check(address) == AddressCheck.Valid;

    public static AddressCheck Check(string? address)
    {
        if (!TryPrefix(address, out var prefix))
            return AddressCheck.Malformed;
        if (!KaspaNetworks.IsKnownPrefix(prefix))
            return AddressCheck.Malformed;

        var status = KaspaBech32.Decode(address, out _);
        return status switch
        {
            KaspaBech32Status.Ok => AddressCheck.Valid,
            KaspaBech32Status.BadChecksum => AddressCheck.BadChecksum,
            KaspaBech32Status.BadPayload => AddressCheck.BadPayload,
            _ => AddressCheck.Malformed
        };
    }

    public static bool TryDecode(string? address, out KaspaAddressInfo info)
    {
        info = default;
        if (Check(address) != AddressCheck.Valid)
            return false;
        return KaspaBech32.Decode(address, out info) == KaspaBech32Status.Ok;
    }

    public static bool TryDetectNetwork(string address, out KaspaNetwork network)
    {
        network = default;
        if (!TryDecode(address, out var info))
            return false;
        return KaspaNetworks.TryFromPrefix(info.Prefix, out network);
    }

    private static bool TryPrefix(string? address, out string prefix)
    {
        prefix = "";
        if (string.IsNullOrWhiteSpace(address))
            return false;
        address = address.Trim();
        var colon = address.IndexOf(':');
        if (colon <= 0)
            return false;
        prefix = address[..colon];
        return true;
    }
}

public enum AddressCheck
{
    Valid,
    Malformed,
    BadChecksum,
    BadPayload
}
