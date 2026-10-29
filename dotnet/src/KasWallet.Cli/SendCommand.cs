using System.Security.Cryptography;
using System.Text.Json;
using KasWallet.Core.Consensus;

namespace KasWallet.Cli;

public sealed record SendOptions(
    string FromKey,
    string To,
    long AmountSompi,
    long? FixedFeeSompi,
    ulong FeeRate,
    bool Broadcast,
    string? Passphrase = null,
    bool SweepAll = false,
    CoinSelectionStrategy CoinSelection = CoinSelectionStrategy.LargestFirst);

/// <summary>Implementation of <c>kaswallet send</c>. stdout carries the submit JSON, stderr the diagnostics.</summary>
public static class SendCommand
{
    /// <returns>Process exit code.</returns>
    public static async Task<int> RunAsync(
        SendOptions options,
        string? api,
        TextWriter stdout,
        TextWriter stderr,
        HttpClient? http = null)
    {
        byte[]? key = null;
        try
        {
            if (options.FixedFeeSompi is < 0)
                throw new ArgumentException("--fee must be >= 0.");
            if (options.SweepAll && options.AmountSompi != 0)
                throw new ArgumentException("--all cannot be combined with --amount.");
            if (options.SweepAll && options.CoinSelection != CoinSelectionStrategy.LargestFirst)
                throw new ArgumentException("--coin-selection does not apply to --all (a sweep spends every UTXO it can).");
            key = ConsensusTransactionFactory.SigningKeyFrom(options.FromKey, options.Passphrase);
            if (!AddressValidator.TryDetectNetwork(options.To, out var network))
                throw new ArgumentException("Invalid --to address.");

            var fromAddress = ConsensusTransactionFactory.AddressFromPrivateKey(key, network);
            stderr.WriteLine($"from={fromAddress}");
            using var client = new KaspaRestClient(api, http);
            var utxos = await client.GetUtxosAsync(fromAddress);
            var feeOverride = options.FixedFeeSompi is { } fixedFee ? (ulong)fixedFee : (ulong?)null;
            SweepResult? sweep = null;
            BuiltTransaction built;
            if (options.SweepAll)
            {
                sweep = ConsensusTransactionFactory.BuildSweep(utxos, options.To, key, options.FeeRate, feeOverride);
                built = sweep.Built;
            }
            else
            {
                built = ConsensusTransactionFactory.BuildWithFeeRate(
                    utxos,
                    options.To,
                    options.AmountSompi,
                    fromAddress,
                    key,
                    options.FeeRate,
                    feeOverride,
                    options.CoinSelection);
            }

            var json = TransactionJson.ToRestSubmitJson(built.Transaction);
            stdout.WriteLine(json);
            foreach (var line in FeeReport.Lines(built))
                stderr.WriteLine(line);
            if (!options.SweepAll)
            {
                stderr.WriteLine($"coin_selection={(options.CoinSelection == CoinSelectionStrategy.SmallestFirst ? "smallest-first" : "largest-first")}");
                stderr.WriteLine($"inputs={built.Transaction.Inputs.Count}");
            }
            if (sweep is not null)
            {
                stderr.WriteLine($"sweep_sent_sompi={sweep.SentSompi}");
                stderr.WriteLine($"sweep_inputs={sweep.InputsUsed}");
                stderr.WriteLine($"sweep_leftover_inputs={sweep.LeftoverInputs}");
                stderr.WriteLine($"sweep_leftover_sompi={sweep.LeftoverSompi}");
                if (sweep.LeftoverInputs > 0)
                    stderr.WriteLine("note=some UTXOs did not fit into one transaction or were not worth their fee; run again after this one confirms to sweep the rest");
            }

            if (!options.Broadcast)
            {
                stderr.WriteLine("dry-run: not broadcast (pass --broadcast to POST)");
                return 0;
            }

            var responseBody = await client.SubmitTransactionAsync(json);
            stdout.WriteLine(responseBody);
            return SubmitLooksAccepted(responseBody) ? 0 : 1;
        }
        catch (Exception ex)
        {
            stderr.WriteLine(ex.Message);
            return 1;
        }
        finally
        {
            if (key is not null)
                CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <summary>Parses <c>largest-first</c> / <c>smallest-first</c> (case-insensitive).</summary>
    public static CoinSelectionStrategy ParseCoinSelection(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return CoinSelectionStrategy.LargestFirst;
        return value.Trim().ToLowerInvariant() switch
        {
            "largest-first" or "largest" => CoinSelectionStrategy.LargestFirst,
            "smallest-first" or "smallest" => CoinSelectionStrategy.SmallestFirst,
            _ => throw new ArgumentException($"Unknown --coin-selection '{value}'. Use largest-first or smallest-first."),
        };
    }

    public static bool SubmitLooksAccepted(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return false;
            if (doc.RootElement.TryGetProperty("error", out var error) &&
                error.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined &&
                !(error.ValueKind == JsonValueKind.String && string.IsNullOrEmpty(error.GetString())))
                return false;
            return doc.RootElement.TryGetProperty("transactionId", out var id) &&
                   id.ValueKind == JsonValueKind.String &&
                   !string.IsNullOrEmpty(id.GetString());
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
