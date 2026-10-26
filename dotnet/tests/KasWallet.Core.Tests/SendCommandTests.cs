using System.Net;
using System.Text;
using System.Text.Json;
using KasWallet;
using KasWallet.Cli;
using KasWallet.Core.Consensus;
using Xunit;

namespace KasWallet.Core.Tests;

/// <summary>End-to-end <c>send</c> against a fake REST API. Nothing leaves the process.</summary>
public class SendCommandTests
{
    private const string Destination = "kaspatest:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqhqrxplya";
    private const string KeyHex = "0000000000000000000000000000000000000000000000000000000000000001";

    private static string From()
    {
        var key = Convert.FromHexString(KeyHex);
        return ConsensusTransactionFactory.AddressFromPrivateKey(key, KaspaNetwork.Testnet10);
    }

    private sealed class FakeApi : HttpMessageHandler
    {
        private readonly long[] _utxoAmounts;
        public List<(HttpMethod Method, string Path)> Requests { get; } = new();

        public FakeApi(params long[] utxoAmounts) => _utxoAmounts = utxoAmounts;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Method, request.RequestUri!.AbsolutePath));
            string body;
            if (request.Method == HttpMethod.Get && request.RequestUri.AbsolutePath.EndsWith("/utxos", StringComparison.Ordinal))
            {
                var entries = _utxoAmounts.Select((a, i) => new
                {
                    address = From(),
                    outpoint = new { transactionId = (i + 1).ToString("x2") + new string('0', 62), index = 0 },
                    utxoEntry = new { amount = a, isCoinbase = false, blockDaaScore = 1 }
                });
                body = JsonSerializer.Serialize(entries);
            }
            else
            {
                body = "{\"transactionId\":\"" + new string('a', 64) + "\"}";
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private static async Task<(int Code, string Out, string Err, FakeApi Api)> Run(SendOptions options, params long[] utxos)
    {
        var api = new FakeApi(utxos);
        using var http = new HttpClient(api);
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = await SendCommand.RunAsync(options, "http://fake.invalid/", stdout, stderr, http);
        return (code, stdout.ToString(), stderr.ToString(), api);
    }

    private static SendOptions Options(long amount, ulong rate = 100, long? fee = null, bool broadcast = false)
        => new(KeyHex, Destination, amount, fee, rate, broadcast);

    [Fact]
    public async Task Dry_run_prints_json_mass_and_fee_and_does_not_post()
    {
        var (code, stdout, stderr, api) = await Run(Options(500_000_000), 5_675_155_239);

        Assert.Equal(0, code);
        using var doc = JsonDocument.Parse(stdout);
        Assert.True(doc.RootElement.TryGetProperty("transaction", out _));
        Assert.Contains("mass=2036", stderr);
        Assert.Contains("fee_rate=100", stderr);
        Assert.Contains("fee_sompi=203600", stderr);
        Assert.Contains("fee_kas=0.00203600", stderr);
        Assert.Contains("dry-run: not broadcast", stderr);
        Assert.DoesNotContain(api.Requests, r => r.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task Fee_rate_option_scales_the_fee()
    {
        var (code, _, stderr, _) = await Run(Options(500_000_000, rate: 250), 5_675_155_239);
        Assert.Equal(0, code);
        Assert.Contains("fee_rate=250", stderr);
        Assert.Contains($"fee_sompi={2036 * 250}", stderr);
    }

    [Fact]
    public async Task Storage_dominated_amount_reports_the_higher_mass()
    {
        var (code, _, stderr, _) = await Run(Options(12_000_000), 5_675_155_239);
        Assert.Equal(0, code);
        var massLine = stderr.Split('\n').Select(l => l.Trim()).First(l => l.StartsWith("mass=", StringComparison.Ordinal));
        Assert.InRange(long.Parse(massLine["mass=".Length..]), 83_333, 83_340);
    }

    [Fact]
    public async Task Explicit_fee_overrides_the_estimate()
    {
        var (code, _, stderr, _) = await Run(Options(500_000_000, fee: 1_000_000), 5_675_155_239);
        Assert.Equal(0, code);
        Assert.Contains("fee_sompi=1000000", stderr);
        Assert.Contains("mass=2036", stderr);
    }

    [Fact]
    public async Task Fee_rate_below_minimum_fails_without_posting()
    {
        var (code, stdout, stderr, api) = await Run(Options(500_000_000, rate: 1), 5_675_155_239);
        Assert.Equal(1, code);
        Assert.Equal("", stdout);
        Assert.Contains("below the network minimum", stderr);
        Assert.DoesNotContain(api.Requests, r => r.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task Insufficient_funds_fails()
    {
        var (code, _, stderr, _) = await Run(Options(500_000_000), 100_000_000);
        Assert.Equal(1, code);
        Assert.Contains("Insufficient funds", stderr);
    }

    [Fact]
    public async Task Output_never_contains_the_private_key()
    {
        var (_, stdout, stderr, _) = await Run(Options(500_000_000), 5_675_155_239);
        Assert.DoesNotContain(KeyHex, stdout + stderr);
    }

    [Fact]
    public async Task Broadcast_flag_posts_exactly_once_to_the_fake_api()
    {
        var (code, stdout, _, api) = await Run(Options(500_000_000, broadcast: true), 5_675_155_239);
        Assert.Equal(0, code);
        Assert.Single(api.Requests, r => r.Method == HttpMethod.Post);
        Assert.Contains(new string('a', 64), stdout);
    }
}
