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

    public string GenerateReceiveAddress(string mnemonic, int index = 0, string? passphrase = null)
        => GenerateReceiveAddress(mnemonic, index, Network, passphrase);

    public static string GenerateReceiveAddress(string mnemonic, int index, KaspaNetwork network, string? passphrase = null)
        => SchnorrAddress.GenerateReceiveAddress(mnemonic, index, network, passphrase);

    public void Dispose() => _rpc.Dispose();
}
