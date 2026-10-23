namespace KasWallet;

/// <summary>
/// Selects UTXOs and drafts a payment. Consensus Schnorr sighash signing is not
/// applied; signature fields on <see cref="SignedTransactionDraft"/> stay empty.
/// </summary>
public static class TransactionBuilder
{
    public const string UnsignedNotes = "Unsigned draft only — Schnorr sign/broadcast not enabled.";

    public const string DraftNotes =
        "Experimental draft. Schnorr signature slots are empty placeholders. " +
        "BIP-340 message signing exists, but Kaspa consensus sighash is not applied. Do not broadcast to mainnet.";

    public static UnsignedPayment BuildPayment(
        KaspaNetwork network,
        IReadOnlyList<UtxoEntry> availableUtxos,
        string destination,
        long amountSompi,
        string? changeAddress,
        long feeSompi = 10_000)
    {
        var plan = Plan(availableUtxos, destination, amountSompi, changeAddress, feeSompi);
        return new UnsignedPayment(
            network,
            plan.Inputs,
            destination,
            amountSompi,
            plan.ChangeSompi > 0 ? changeAddress : null,
            plan.ChangeSompi,
            feeSompi,
            UnsignedNotes);
    }

    public static SignedTransactionDraft BuildDraft(
        KaspaNetwork network,
        IReadOnlyList<UtxoEntry> availableUtxos,
        string destination,
        long amountSompi,
        string? changeAddress,
        string? changeDerivationPath = null,
        long feeSompi = 10_000)
    {
        var plan = Plan(availableUtxos, destination, amountSompi, changeAddress, feeSompi);
        var outputs = new List<DraftOutput>
        {
            new(destination, amountSompi, SchnorrAddress.ScriptForAddress(destination))
        };
        string? usedChange = null;
        if (plan.ChangeSompi > 0)
        {
            usedChange = changeAddress;
            outputs.Add(new(changeAddress!, plan.ChangeSompi, SchnorrAddress.ScriptForAddress(changeAddress!)));
        }

        var inputs = plan.Inputs.Select(u => new DraftInput(
            u.TransactionId,
            u.Index,
            u.AmountSompi,
            u.Address,
            SchnorrSignatureHex: null,
            SignatureScriptHex: null)).ToList();

        return new SignedTransactionDraft(
            network,
            inputs,
            outputs,
            feeSompi,
            usedChange,
            usedChange is null ? null : changeDerivationPath,
            plan.ChangeSompi,
            HasSchnorrSignatures: false,
            DraftNotes);
    }

    public static SignedTransactionDraft BuildDraftFromMnemonic(
        string mnemonic,
        int changeIndex,
        KaspaNetwork network,
        IReadOnlyList<UtxoEntry> availableUtxos,
        string destination,
        long amountSompi,
        string? passphrase = null,
        long feeSompi = 10_000)
    {
        var changeAddress = SchnorrAddress.GenerateChangeAddress(mnemonic, changeIndex, network, passphrase);
        return BuildDraft(
            network,
            availableUtxos,
            destination,
            amountSompi,
            changeAddress,
            KeyDerivation.ChangePath(changeIndex),
            feeSompi);
    }

    private static CoinSelection Plan(
        IReadOnlyList<UtxoEntry> availableUtxos,
        string destination,
        long amountSompi,
        string? changeAddress,
        long feeSompi)
    {
        if (!AddressValidator.IsValid(destination))
            throw new ArgumentException("Invalid destination address.", nameof(destination));
        if (amountSompi <= 0)
            throw new ArgumentOutOfRangeException(nameof(amountSompi));
        if (feeSompi < 0)
            throw new ArgumentOutOfRangeException(nameof(feeSompi));

        var need = amountSompi + feeSompi;
        var selected = new List<UtxoEntry>();
        long total = 0;
        foreach (var utxo in availableUtxos.OrderByDescending(x => x.AmountSompi))
        {
            selected.Add(utxo);
            total += utxo.AmountSompi;
            if (total >= need)
                break;
        }

        if (total < need)
            throw new InvalidOperationException($"Insufficient funds: have {total} sompi, need {need}.");

        var change = total - need;
        if (change > 0 && string.IsNullOrWhiteSpace(changeAddress))
            throw new InvalidOperationException("Change is required but changeAddress was not provided.");
        if (change > 0 && !AddressValidator.IsValid(changeAddress))
            throw new ArgumentException("Invalid change address.", nameof(changeAddress));

        return new CoinSelection(selected, change);
    }

    private readonly record struct CoinSelection(List<UtxoEntry> Inputs, long ChangeSompi);
}
