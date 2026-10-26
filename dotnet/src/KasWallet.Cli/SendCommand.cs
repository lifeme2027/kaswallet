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
    string? Passphrase = null);

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
            key = ConsensusTransactionFactory.SigningKeyFrom(options.FromKey, options.Passphrase);
            if (!AddressValidator.TryDetectNetwork(options.To, out var network))
                throw new ArgumentException("Invalid --to address.");

            var fromAddress = ConsensusTransactionFactory.AddressFromPrivateKey(key, network);
            stderr.WriteLine($"from={fromAddress}");
            using var client = new KaspaRestClient(api, http);
            var utxos = await client.GetUtxosAsync(fromAddress);
            var built = ConsensusTransactionFactory.BuildWithFeeRate(
                utxos,
                options.To,
                options.AmountSompi,
                fromAddress,
                key,
                options.FeeRate,
                options.FixedFeeSompi is { } fixedFee ? (ulong)fixedFee : null);

            var json = TransactionJson.ToRestSubmitJson(built.Transaction);
            stdout.WriteLine(json);
            foreach (var line in FeeReport.Lines(built))
                stderr.WriteLine(line);
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
