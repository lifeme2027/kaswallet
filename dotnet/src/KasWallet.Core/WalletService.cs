namespace KasWallet;

public sealed class WalletService : IDisposable
{
    private readonly KaspaRestClient _rpc;

    public WalletService(KaspaNetwork network, KaspaRestClient? rpc = null)
    {
        Network = network;
        _rpc = rpc ?? new KaspaRestClient();
    }

    public KaspaNetwork Network { get; }

    public Task<AddressBalance> GetBalanceAsync(string address, CancellationToken ct = default)
        => _rpc.GetBalanceAsync(address, ct);

    public Task<IReadOnlyList<UtxoEntry>> GetUtxosAsync(string address, CancellationToken ct = default)
        => _rpc.GetUtxosAsync(address, ct);

    public void Dispose() => _rpc.Dispose();
}
