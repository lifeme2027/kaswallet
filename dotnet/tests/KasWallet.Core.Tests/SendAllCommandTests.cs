using System.Net;
using System.Text;
using System.Text.Json;
using KasWallet;
using KasWallet.Cli;
using KasWallet.Core.Consensus;
using Xunit;

namespace KasWallet.Core.Tests;

/// <summary><c>send --all</c> against an in-process fake REST API. Nothing leaves the process.</summary>
public class SendAllCommandTests
{
    private const string Destination = "kaspatest:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqhqrxplya";
    private const string KeyHex = "0000000000000000000000000000000000000000000000000000000000000001";
    private const long Kas = 100_000_000;

    private static string From()
        => ConsensusTransactionFactory.AddressFromPrivateKey(Convert.FromHexString(KeyHex), KaspaNetwork.Testnet10);

    private sealed class FakeApi : HttpMessageHandler
    {
        private readonly long[] _amounts;
        public List<HttpMethod> Methods { get; } = new();

        public FakeApi(long[] amounts) => _amounts = amounts;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Methods.Add(request.Method);
            string body;
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("/utxos", StringComparison.Ordinal))
            {
                body = JsonSerializer.Serialize(_amounts.Select((a, i) => new
                {
                    address = From(),
                    outpoint = new { transactionId = (i + 1).ToString("x4") + new string('0', 60), index = 0 },
                    utxoEntry = new { amount = a, isCoinbase = false, blockDaaScore = 1 }
                }));
            }
            else
            {
                body = "{\"transactionId\":\"" + new string('b', 64) + "\"}";
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

    private static SendOptions All(long amount = 0, long? fee = null, bool broadcast = false)
        => new(KeyHex, Destination, amount, fee, 100, broadcast, null, SweepAll: true);

    [Fact]
    public async Task Dry_run_spends_every_utxo_into_one_output()
    {
        var (code, stdout, stderr, api) = await Run(All(), 3 * Kas, 2 * Kas);

        Assert.Equal(0, code);
        using var doc = JsonDocument.Parse(stdout);
        var tx = doc.RootElement.GetProperty("transaction");
        Assert.Equal(2, tx.GetProperty("inputs").GetArrayLength());
        Assert.Equal(1, tx.GetProperty("outputs").GetArrayLength());
        Assert.Contains("change_sompi=0", stderr);
        Assert.Contains("sweep_inputs=2", stderr);
        Assert.Contains("sweep_leftover_inputs=0", stderr);
        Assert.DoesNotContain("note=", stderr);
        Assert.DoesNotContain(HttpMethod.Post, api.Methods);

        var fee = long.Parse(stderr.Split('\n').Select(l => l.Trim()).First(l => l.StartsWith("fee_sompi=", StringComparison.Ordinal))["fee_sompi=".Length..]);
        var sent = long.Parse(stderr.Split('\n').Select(l => l.Trim()).First(l => l.StartsWith("sweep_sent_sompi=", StringComparison.Ordinal))["sweep_sent_sompi=".Length..]);
        Assert.Equal(5 * Kas, sent + fee);
    }

    [Fact]
    public async Task Leftover_utxos_are_reported_with_a_note()
    {
        var (code, _, stderr, _) = await Run(All(), Enumerable.Repeat(10 * Kas, 200).ToArray());
        Assert.Equal(0, code);
        Assert.DoesNotContain("sweep_leftover_inputs=0", stderr);
        Assert.Contains("sweep_leftover_inputs=", stderr);
        Assert.Contains("note=some UTXOs", stderr);
    }

    [Fact]
    public async Task Amount_together_with_all_is_rejected_before_any_request()
    {
        var (code, stdout, stderr, api) = await Run(All(amount: Kas), 5 * Kas);
        Assert.Equal(1, code);
        Assert.Equal("", stdout);
        Assert.Contains("--all cannot be combined with --amount", stderr);
        Assert.Empty(api.Methods);
    }

    [Fact]
    public async Task Fixed_fee_is_honoured()
    {
        var (code, _, stderr, _) = await Run(All(fee: 40_000), 5 * Kas);
        Assert.Equal(0, code);
        Assert.Contains("fee_sompi=40000", stderr);
        Assert.Contains($"sweep_sent_sompi={5 * Kas - 40_000}", stderr);
    }

    [Fact]
    public async Task Broadcast_posts_once_and_never_prints_the_key()
    {
        var (code, stdout, stderr, api) = await Run(All(broadcast: true), 5 * Kas);
        Assert.Equal(0, code);
        Assert.Single(api.Methods, m => m == HttpMethod.Post);
        Assert.Contains(new string('b', 64), stdout);
        Assert.DoesNotContain(KeyHex, stdout + stderr);
    }

    [Fact]
    public async Task Empty_wallet_fails_without_posting()
    {
        var (code, _, stderr, api) = await Run(All(broadcast: true));
        Assert.Equal(1, code);
        Assert.Contains("No spendable", stderr);
        Assert.DoesNotContain(HttpMethod.Post, api.Methods);
    }
}
