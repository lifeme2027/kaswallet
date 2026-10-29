using System.Net;
using System.Text;
using System.Text.Json;
using KasWallet;
using KasWallet.Cli;
using KasWallet.Core.Consensus;
using Xunit;

namespace KasWallet.Core.Tests;

/// <summary><see cref="CoinSelectionStrategy"/> in BuildWithFeeRate and <c>send --coin-selection</c>. Network is faked.</summary>
public class CoinSelectionTests
{
    private const string Destination = "kaspatest:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqhqrxplya";
    private const string KeyHex = "0000000000000000000000000000000000000000000000000000000000000001";
    private const long Kas = 100_000_000;

    private static byte[] Key() => Convert.FromHexString(KeyHex);

    private static string From() => ConsensusTransactionFactory.AddressFromPrivateKey(Key(), KaspaNetwork.Testnet10);

    private static string Id(int marker) => marker.ToString("x2") + new string('0', 62);

    private static List<KasWallet.UtxoEntry> Utxos(params long[] amounts)
        => amounts.Select((a, i) => new KasWallet.UtxoEntry(From(), Id(i + 1), 0, a, false, 1)).ToList();

    private static HashSet<long> SpentAmounts(BuiltTransaction built, List<KasWallet.UtxoEntry> utxos)
    {
        var byId = utxos.ToDictionary(u => u.TransactionId, u => u.AmountSompi);
        return built.Transaction.Inputs
            .Select(i => byId[Convert.ToHexString(i.PreviousOutpoint.TransactionId).ToLowerInvariant()])
            .ToHashSet();
    }

    [Fact]
    public void Default_is_largest_first_and_uses_one_input()
    {
        var utxos = Utxos(2 * Kas, 3 * Kas, 50 * Kas);
        var built = ConsensusTransactionFactory.BuildWithFeeRate(utxos, Destination, 4 * Kas, From(), Key());
        Assert.Single(built.Transaction.Inputs);
        Assert.Equal(new HashSet<long> { 50 * Kas }, SpentAmounts(built, utxos));
    }

    [Fact]
    public void Smallest_first_spends_small_utxos_before_large_ones()
    {
        var utxos = Utxos(50 * Kas, 3 * Kas, 2 * Kas);
        var built = ConsensusTransactionFactory.BuildWithFeeRate(
            utxos, Destination, 4 * Kas, From(), Key(), coinSelection: CoinSelectionStrategy.SmallestFirst);
        Assert.Equal(2, built.Transaction.Inputs.Count);
        Assert.Equal(new HashSet<long> { 2 * Kas, 3 * Kas }, SpentAmounts(built, utxos));
        Assert.Equal((ulong)(5 * Kas - 4 * Kas) - built.FeeSompi, built.ChangeSompi);
    }

    [Fact]
    public void Smallest_first_pays_more_compute_mass_than_largest_first()
    {
        var utxos = Utxos(50 * Kas, 3 * Kas, 2 * Kas);
        var largest = ConsensusTransactionFactory.BuildWithFeeRate(utxos, Destination, 4 * Kas, From(), Key());
        var smallest = ConsensusTransactionFactory.BuildWithFeeRate(
            utxos, Destination, 4 * Kas, From(), Key(), coinSelection: CoinSelectionStrategy.SmallestFirst);
        Assert.True(smallest.Mass.ComputeMass > largest.Mass.ComputeMass);
    }

    [Fact]
    public void Order_is_deterministic_for_equal_amounts()
    {
        var utxos = Utxos(Kas, Kas, Kas);
        utxos.Reverse();
        var ordered = ConsensusTransactionFactory.OrderForSelection(utxos, CoinSelectionStrategy.SmallestFirst);
        Assert.Equal(new[] { Id(1), Id(2), Id(3) }, ordered.Select(u => u.TransactionId));
    }

    [Fact]
    public void Unknown_strategy_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ConsensusTransactionFactory.BuildWithFeeRate(
            Utxos(10 * Kas), Destination, Kas, From(), Key(), coinSelection: (CoinSelectionStrategy)42));
    }

    [Theory]
    [InlineData(null, CoinSelectionStrategy.LargestFirst)]
    [InlineData("largest-first", CoinSelectionStrategy.LargestFirst)]
    [InlineData("Smallest-First", CoinSelectionStrategy.SmallestFirst)]
    [InlineData(" smallest ", CoinSelectionStrategy.SmallestFirst)]
    public void Parse_accepts_known_names(string? text, CoinSelectionStrategy expected)
        => Assert.Equal(expected, SendCommand.ParseCoinSelection(text));

    [Fact]
    public void Parse_rejects_unknown_name()
    {
        var ex = Assert.Throws<ArgumentException>(() => SendCommand.ParseCoinSelection("random"));
        Assert.Contains("largest-first", ex.Message);
    }

    [Fact]
    public async Task Send_smallest_first_is_dry_run_and_reports_strategy()
    {
        var api = new FakeApi(50 * Kas, 3 * Kas, 2 * Kas);
        using var http = new HttpClient(api);
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var options = new SendOptions(KeyHex, Destination, 4 * Kas, null, 100, Broadcast: false,
            CoinSelection: CoinSelectionStrategy.SmallestFirst);

        var code = await SendCommand.RunAsync(options, "http://fake.invalid/", stdout, stderr, http);

        Assert.Equal(0, code);
        Assert.Contains("coin_selection=smallest-first", stderr.ToString());
        Assert.Contains("inputs=2", stderr.ToString());
        Assert.Contains("dry-run", stderr.ToString());
        Assert.DoesNotContain(api.Requests, r => r.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task Send_all_with_smallest_first_is_rejected_before_any_request()
    {
        var api = new FakeApi(10 * Kas);
        using var http = new HttpClient(api);
        var stderr = new StringWriter();
        var options = new SendOptions(KeyHex, Destination, 0, null, 100, Broadcast: false,
            SweepAll: true, CoinSelection: CoinSelectionStrategy.SmallestFirst);

        var code = await SendCommand.RunAsync(options, "http://fake.invalid/", new StringWriter(), stderr, http);

        Assert.Equal(1, code);
        Assert.Contains("--coin-selection", stderr.ToString());
        Assert.Empty(api.Requests);
    }

    private sealed class FakeApi : HttpMessageHandler
    {
        private readonly long[] _amounts;
        public List<(HttpMethod Method, string Path)> Requests { get; } = new();

        public FakeApi(params long[] amounts) => _amounts = amounts;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Method, request.RequestUri!.AbsolutePath));
            var body = request.Method == HttpMethod.Get
                ? JsonSerializer.Serialize(_amounts.Select((a, i) => new
                {
                    address = From(),
                    outpoint = new { transactionId = Id(i + 1), index = 0 },
                    utxoEntry = new { amount = a, isCoinbase = false, blockDaaScore = 1 }
                }))
                : "{\"transactionId\":\"" + new string('a', 64) + "\"}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
