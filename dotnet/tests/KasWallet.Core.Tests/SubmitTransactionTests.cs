using System.Net;
using System.Text;
using KasWallet;
using Xunit;

namespace KasWallet.Core.Tests;

public class SubmitTransactionTests
{
    [Fact]
    public async Task Posts_draft_to_configured_transactions_endpoint()
    {
        var handler = new CaptureHandler("{\"transactionId\":\"abc123\"}", HttpStatusCode.OK);
        using var http = new HttpClient(handler) { BaseAddress = new Uri(KaspaRestClient.DefaultBaseUrl) };
        using var client = new KaspaRestClient(httpClient: http);
        var draft = SampleDraft();

        var result = await client.SubmitTransactionAsync(draft);

        Assert.True(result.Accepted);
        Assert.Equal("abc123", result.TransactionId);
        Assert.Equal(new Uri("https://api.kaspa.org/transactions"), handler.RequestUri);
        Assert.Contains("\"signatureScript\":\"\"", handler.Body);
        Assert.Contains("\"allowOrphan\":false", handler.Body);
    }

    [Fact]
    public async Task Returns_node_error_without_marking_accepted()
    {
        var handler = new CaptureHandler("{\"error\":\"signature script is empty\"}", HttpStatusCode.BadRequest);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:16710/") };
        using var client = new KaspaRestClient(httpClient: http);

        var result = await client.SubmitTransactionAsync(SampleDraft());

        Assert.False(result.Accepted);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("signature script is empty", result.Error);
        Assert.Equal(new Uri("http://127.0.0.1:16710/transactions"), handler.RequestUri);
    }

    [Fact]
    public async Task Submit_string_posts_json_and_returns_the_response_body()
    {
        var handler = new CaptureHandler("{\"transactionId\":\"zzz\"}", HttpStatusCode.OK);
        using var http = new HttpClient(handler);
        using var client = new KaspaRestClient("https://api-tn10.kaspa.org", http);
        const string json = "{\"transaction\":{},\"allowOrphan\":false}";

        var body = await client.SubmitTransactionAsync(json);

        Assert.Equal("{\"transactionId\":\"zzz\"}", body);
        Assert.Equal(json, handler.Body);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(new Uri("https://api-tn10.kaspa.org/transactions"), handler.RequestUri);
    }

    [Fact]
    public async Task Submit_string_returns_error_body()
    {
        var handler = new CaptureHandler("{\"error\":\"rejected\"}", HttpStatusCode.BadRequest);
        using var http = new HttpClient(handler) { BaseAddress = new Uri(KaspaRestClient.Testnet10BaseUrl) };
        using var client = new KaspaRestClient(httpClient: http);

        var body = await client.SubmitTransactionAsync("{}");

        Assert.Equal("{\"error\":\"rejected\"}", body);
        Assert.Equal(new Uri("https://api-tn10.kaspa.org/transactions"), handler.RequestUri);
    }

    private static SignedTransactionDraft SampleDraft()
    {
        var address = "kaspa:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4e";
        var utxos = new[] { new UtxoEntry(address, "aa", 0, 50_000_000, false, 1) };
        return TransactionBuilder.BuildDraft(
            KaspaNetwork.Mainnet,
            utxos,
            address,
            20_000_000,
            address,
            feeSompi: 1_000);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        private readonly string _response;
        private readonly HttpStatusCode _status;

        public CaptureHandler(string response, HttpStatusCode status)
        {
            _response = response;
            _status = status;
        }

        public Uri? RequestUri { get; private set; }
        public HttpMethod? Method { get; private set; }
        public string Body { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Method = request.Method;
            Body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_response, Encoding.UTF8, "application/json")
            };
        }
    }
}
