namespace KasWallet;

/// <summary>
/// Selects UTXOs and drafts an unsigned payment plan.
/// Schnorr signing + mass limits + broadcast are not implemented yet (pre-alpha).
/// </summary>
public static class TransactionBuilder
{
    public static UnsignedPayment BuildPayment(
        KaspaNetwork network,
        IReadOnlyList<UtxoEntry> availableUtxos,
        string destination,
        long amountSompi,
        string? changeAddress,
        long feeSompi = 10_000)
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
        foreach (var u in availableUtxos.OrderByDescending(x => x.AmountSompi))
        {
            selected.Add(u);
            total += u.AmountSompi;
            if (total >= need)
                break;
        }

        if (total < need)
            throw new InvalidOperationException($"Insufficient funds: have {total} sompi, need {need}.");

        var change = total - need;
        if (change > 0 && string.IsNullOrWhiteSpace(changeAddress))
            throw new InvalidOperationException("Change is required but changeAddress was not provided.");

        return new UnsignedPayment(
            network,
            selected,
            destination,
            amountSompi,
            change > 0 ? changeAddress : null,
            change,
            feeSompi,
            "Unsigned draft only — Schnorr sign/broadcast not enabled.");
    }
}
