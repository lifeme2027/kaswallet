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

public sealed record DraftInput(
    string TransactionId,
    uint Index,
    long AmountSompi,
    string Address,
    string? SchnorrSignatureHex,
    string? SignatureScriptHex);

public sealed record DraftOutput(
    string Address,
    long AmountSompi,
    string ScriptPublicKeyHex);

/// <summary>
/// JSON-serializable payment draft. Schnorr signature fields are placeholders
/// until Kaspa consensus sighash signing exists.
/// </summary>
public sealed record SignedTransactionDraft(
    KaspaNetwork Network,
    IReadOnlyList<DraftInput> Inputs,
    IReadOnlyList<DraftOutput> Outputs,
    long FeeSompi,
    string? ChangeAddress,
    string? ChangeDerivationPath,
    long ChangeSompi,
    bool HasSchnorrSignatures,
    string Notes);

public sealed record SubmitTransactionResult(int StatusCode, string? TransactionId, string? Error, string RawBody)
{
    public bool Accepted =>
        StatusCode is >= 200 and < 300 &&
        !string.IsNullOrEmpty(TransactionId) &&
        string.IsNullOrEmpty(Error);
}
