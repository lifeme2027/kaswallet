using KasWallet;

namespace KasWallet.Cli;

/// <summary>
/// Offline batch address check. One line per input (<c>valid|invalid&lt;TAB&gt;address&lt;TAB&gt;reason</c>)
/// plus a summary. No network access; returns 0 only when every address is valid
/// (and on the expected network, if one is given).
/// </summary>
public static class ValidateBatchCommand
{
    public static int Run(IEnumerable<string> inputs, KaspaNetwork? expected, TextWriter stdout)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(stdout);
        int valid = 0, invalid = 0;
        foreach (var raw in inputs)
        {
            var address = raw?.Trim() ?? "";
            if (address.Length == 0 || address.StartsWith('#'))
                continue;

            var reason = Reason(address, expected);
            if (reason is null)
            {
                valid++;
                stdout.WriteLine($"valid\t{address}");
            }
            else
            {
                invalid++;
                stdout.WriteLine($"invalid\t{address}\t{reason}");
            }
        }

        stdout.WriteLine($"summary total={valid + invalid} valid={valid} invalid={invalid}");
        return valid + invalid > 0 && invalid == 0 ? 0 : 1;
    }

    public static string? Reason(string address, KaspaNetwork? expected)
    {
        switch (AddressValidator.Check(address))
        {
            case AddressCheck.Malformed: return "malformed";
            case AddressCheck.BadChecksum: return "checksum=bad";
            case AddressCheck.BadPayload: return "payload=bad";
        }

        if (expected is { } want
            && (!AddressValidator.TryDetectNetwork(address, out var got) || got != want))
            return $"network_mismatch(expected={KaspaNetworks.DisplayName(want)})";
        return null;
    }
}
