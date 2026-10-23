namespace KasWallet;

public sealed record AddressBalance(string Address, long BalanceSompi, long? OutgoingSompi = null)
{
    public decimal BalanceKas => BalanceSompi / 100_000_000m;
}

public sealed record UtxoEntry(
    string Address,
    string TransactionId,
    uint Index,
    long AmountSompi,
    bool IsCoinbase,
    ulong? BlockDaaScore);

public sealed record UnsignedPayment(
    KaspaNetwork Network,
    IReadOnlyList<UtxoEntry> SelectedInputs,
    string Destination,
    long AmountSompi,
    string? ChangeAddress,
    long ChangeSompi,
    long FeeSompi,
    string Notes);
