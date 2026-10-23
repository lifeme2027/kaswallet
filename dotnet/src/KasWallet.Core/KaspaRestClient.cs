using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KasWallet;

public sealed class KaspaRestClient : IDisposable
{
    /// <summary>
    /// Public Kaspa REST API. Transaction submit is POST <c>{DefaultBaseUrl}transactions</c>
    /// (OpenAPI <c>SubmitTransactionRequest</c> on https://api.kaspa.org/openapi.json).
    /// </summary>
    public const string DefaultBaseUrl = "https://api.kaspa.org/";

    public const string SubmitTransactionPath = "transactions";

    /// <summary>Native subnetwork id (20 zero bytes), hex.</summary>
    public const string NativeSubnetworkId = "0000000000000000000000000000000000000000";

    private readonly HttpClient _http;
    private readonly bool _ownsClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public KaspaRestClient(string? baseUrl = null, HttpClient? httpClient = null)
    {
        if (httpClient is null)
        {
            _http = new HttpClient { BaseAddress = new Uri(baseUrl ?? DefaultBaseUrl) };
            _ownsClient = true;
        }
        else
        {
            _http = httpClient;
            if (_http.BaseAddress is null && baseUrl is not null)
                _http.BaseAddress = new Uri(baseUrl);
            _ownsClient = false;
        }
    }

    public async Task<AddressBalance> GetBalanceAsync(string address, CancellationToken ct = default)
    {
        if (!AddressValidator.IsValid(address))
            throw new ArgumentException("Invalid Kaspa address.", nameof(address));

        using var response = await _http.GetAsync($"addresses/{Uri.EscapeDataString(address)}/balance", ct);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<BalanceDto>(JsonOptions, ct)
                  ?? throw new InvalidOperationException("Empty balance response.");
        return new AddressBalance(address, dto.Balance, dto.Outgoing);
    }

    public async Task<IReadOnlyList<UtxoEntry>> GetUtxosAsync(string address, CancellationToken ct = default)
    {
        if (!AddressValidator.IsValid(address))
            throw new ArgumentException("Invalid Kaspa address.", nameof(address));

        using var response = await _http.GetAsync($"addresses/{Uri.EscapeDataString(address)}/utxos", ct);
        response.EnsureSuccessStatusCode();
        var list = await response.Content.ReadFromJsonAsync<List<UtxoDto>>(JsonOptions, ct) ?? [];
        return list.Select(u =>
        {
            var outpoint = u.Outpoint;
            var entry = u.UtxoEntry;
            return new UtxoEntry(
                address,
                outpoint?.TransactionId ?? "",
                outpoint?.Index ?? 0,
                entry?.Amount ?? 0,
                entry?.IsCoinbase ?? false,
                entry?.BlockDaaScore);
        }).ToList();
    }

    /// <summary>
    /// Builds the JSON body for <c>POST /transactions</c>. Empty signature scripts
    /// are sent as-is. Nodes reject those drafts; this is not a mainnet broadcast.
    /// </summary>
    public static string BuildSubmitJson(SignedTransactionDraft draft)
    {
        var payload = new
        {
            transaction = new
            {
                version = 0,
                inputs = draft.Inputs.Select(input => new
                {
                    previousOutpoint = new
                    {
                        transactionId = input.TransactionId,
                        index = input.Index
                    },
                    signatureScript = input.SignatureScriptHex ?? "",
                    sequence = 0,
                    sigOpCount = 1
                }).ToArray(),
                outputs = draft.Outputs.Select(output => new
                {
                    amount = output.AmountSompi,
                    scriptPublicKey = new
                    {
                        version = 0,
                        scriptPublicKey = output.ScriptPublicKeyHex
                    }
                }).ToArray(),
                lockTime = 0,
                subnetworkId = NativeSubnetworkId
            },
            allowOrphan = false
        };
        return JsonSerializer.Serialize(payload);
    }

    /// <summary>
    /// POST <see cref="BuildSubmitJson"/> to <c>{baseUrl}/transactions</c>.
    /// Default URL: https://api.kaspa.org/transactions.
    /// Unsigned placeholder drafts are not consensus-valid; check <see cref="SubmitTransactionResult.Accepted"/>.
    /// </summary>
    public async Task<SubmitTransactionResult> SubmitTransactionAsync(SignedTransactionDraft draft, CancellationToken ct = default)
    {
        if (_http.BaseAddress is null)
            throw new InvalidOperationException(
                $"HttpClient.BaseAddress is required. Use {DefaultBaseUrl} so POST {SubmitTransactionPath} hits {DefaultBaseUrl}{SubmitTransactionPath}.");

        var json = BuildSubmitJson(draft);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync(SubmitTransactionPath, content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        SubmitResponseDto? dto = null;
        try
        {
            dto = JsonSerializer.Deserialize<SubmitResponseDto>(body, JsonOptions);
        }
        catch (JsonException)
        {
            // Non-JSON error pages are returned in RawBody.
        }

        return new SubmitTransactionResult((int)response.StatusCode, dto?.TransactionId, dto?.Error, body);
    }

    public void Dispose()
    {
        if (_ownsClient)
            _http.Dispose();
    }

    private sealed class BalanceDto
    {
        [JsonPropertyName("address")] public string? Address { get; set; }
        [JsonPropertyName("balance")] public long Balance { get; set; }
        [JsonPropertyName("outgoing")] public long? Outgoing { get; set; }
    }

    private sealed class UtxoDto
    {
        [JsonPropertyName("address")] public string? Address { get; set; }
        [JsonPropertyName("outpoint")] public OutpointDto? Outpoint { get; set; }
        [JsonPropertyName("utxoEntry")] public UtxoEntryDto? UtxoEntry { get; set; }
    }

    private sealed class OutpointDto
    {
        [JsonPropertyName("transactionId")] public string? TransactionId { get; set; }
        [JsonPropertyName("index")] public uint Index { get; set; }
    }

    private sealed class UtxoEntryDto
    {
        [JsonPropertyName("amount")] public long Amount { get; set; }
        [JsonPropertyName("isCoinbase")] public bool IsCoinbase { get; set; }
        [JsonPropertyName("blockDaaScore")] public ulong? BlockDaaScore { get; set; }
    }

    private sealed class SubmitResponseDto
    {
        [JsonPropertyName("transactionId")] public string? TransactionId { get; set; }
        [JsonPropertyName("error")] public string? Error { get; set; }
    }
}
