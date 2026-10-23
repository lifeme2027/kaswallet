using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KasWallet;

public sealed class KaspaRestClient : IDisposable
{
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
            _http = new HttpClient { BaseAddress = new Uri(baseUrl ?? "https://api.kaspa.org/") };
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
}
