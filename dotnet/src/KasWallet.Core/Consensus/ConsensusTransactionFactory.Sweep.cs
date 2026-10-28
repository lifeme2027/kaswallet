using KasWallet;

namespace KasWallet.Core.Consensus;

/// <summary>Outcome of <see cref="ConsensusTransactionFactory.BuildSweep"/>.</summary>
/// <param name="Built">The signed single-output transaction with its mass breakdown and fee.</param>
/// <param name="InputsUsed">Number of UTXOs spent.</param>
/// <param name="LeftoverInputs">Number of UTXOs not spent (mass limit, input cap, or not worth their own fee).</param>
/// <param name="LeftoverSompi">Total value of the unspent UTXOs.</param>
public sealed record SweepResult(BuiltTransaction Built, int InputsUsed, int LeftoverInputs, long LeftoverSompi)
{
    /// <summary>Sompi delivered to the destination (inputs minus fee).</summary>
    public ulong SentSompi => Built.Transaction.Outputs[0].Value;
}

public static partial class ConsensusTransactionFactory
{
    /// <summary>
    /// Builds and signs a "send everything" transaction: spends as many UTXOs as fit into one standard
    /// transaction and pays <c>sum(inputs) - fee</c> to <paramref name="destination"/> in a single output (no change).
    /// </summary>
    /// <remarks>
    /// UTXOs are considered largest-first. An input is left out when adding it would push the transaction over
    /// <see cref="MassCalculator.MaxStandardTransactionMass"/> (selection stops there), or when it would not
    /// increase the amount delivered, i.e. its value does not cover the extra fee it causes (it is skipped).
    /// Leftover UTXOs are reported so the caller can sweep again. The fee is <c>mass * feeRate</c> unless
    /// <paramref name="feeOverrideSompi"/> is given; the output shrinks as the fee grows, so the fee is solved by iteration.
    /// </remarks>
    /// <param name="maxInputs">Optional cap on the number of inputs (at least 1).</param>
    public static SweepResult BuildSweep(
        IReadOnlyList<KasWallet.UtxoEntry> availableUtxos,
        string destination,
        ReadOnlySpan<byte> privateKey,
        ulong feeRate = MassCalculator.MinimumFeeRate,
        ulong? feeOverrideSompi = null,
        int? maxInputs = null)
    {
        if (availableUtxos is null)
            throw new ArgumentNullException(nameof(availableUtxos));
        if (destination is null)
            throw new ArgumentNullException(nameof(destination));
        if (privateKey.Length != 32)
            throw new ArgumentException("Private key must be 32 bytes.", nameof(privateKey));
        if (feeRate < MassCalculator.MinimumFeeRate)
            throw new ArgumentOutOfRangeException(nameof(feeRate), $"Fee rate {feeRate} is below the network minimum of {MassCalculator.MinimumFeeRate} sompi/gram.");
        if (maxInputs is < 1)
            throw new ArgumentOutOfRangeException(nameof(maxInputs), "maxInputs must be at least 1.");

        var destinationScript = ScriptFor(destination);
        foreach (var u in availableUtxos)
        {
            if (u is null)
                throw new ArgumentException("UTXO entry is null.", nameof(availableUtxos));
            if (u.AmountSompi < 0)
                throw new ArgumentOutOfRangeException(nameof(availableUtxos), "UTXO amount must be >= 0.");
        }

        var sorted = availableUtxos.Where(u => u.AmountSompi > 0).OrderByDescending(u => u.AmountSompi).ToList();
        if (sorted.Count == 0)
            throw new InvalidOperationException("No spendable UTXOs.");

        var cap = maxInputs ?? int.MaxValue;
        var chosen = new List<KasWallet.UtxoEntry>();
        long chosenTotal = 0;
        SweepPlan? best = null;
        string? failure = null;
        var used = new HashSet<int>();
        for (var i = 0; i < sorted.Count && chosen.Count < cap; i++)
        {
            var trial = new List<KasWallet.UtxoEntry>(chosen) { sorted[i] };
            var trialTotal = checked(chosenTotal + sorted[i].AmountSompi);
            var plan = PlanSweep(trial, trialTotal, destinationScript, feeRate, feeOverrideSompi, out var tooBig, out var why);
            if (tooBig)
            {
                failure = why;
                break; // later inputs are no smaller in compute mass
            }

            if (plan is null)
            {
                failure ??= why;
                continue; // fee exceeds the value so far, or this input costs more than it brings
            }

            if (best is not null && plan.Sent <= best.Sent)
                continue; // not worth its own fee

            chosen = trial;
            chosenTotal = trialTotal;
            best = plan;
            used.Add(i);
        }

        if (best is null)
            throw new InvalidOperationException(failure ?? "Insufficient funds to cover the fee.");

        ConsensusSigner.SignAll(best.Tx, best.Spent, privateKey);
        var built = new BuiltTransaction(best.Tx, best.Mass, feeRate, (ulong)best.Fee, 0);
        var leftover = sorted.Where((_, idx) => !used.Contains(idx)).ToList();
        return new SweepResult(built, chosen.Count, leftover.Count, leftover.Sum(u => u.AmountSompi));
    }

    private sealed record SweepPlan(Transaction Tx, List<UtxoEntry> Spent, TransactionMass Mass, long Fee, ulong Sent);

    /// <summary>Solves fee = f(mass(output = total - fee)) for a fixed input set. Null when infeasible.</summary>
    private static SweepPlan? PlanSweep(
        List<KasWallet.UtxoEntry> chosen,
        long total,
        ScriptPublicKey destinationScript,
        ulong feeRate,
        ulong? feeOverrideSompi,
        out bool tooBig,
        out string? failure)
    {
        tooBig = false;
        failure = null;
        long fee = feeOverrideSompi is { } fixedFee ? checked((long)fixedFee) : 0;
        for (var iteration = 0; iteration < 12; iteration++)
        {
            var sent = total - fee;
            if (sent <= 0)
            {
                failure = $"Insufficient funds: {total} sompi in {chosen.Count} input(s) does not cover the fee of {fee} sompi.";
                return null;
            }

            var tx = AssembleUnsigned(chosen, destinationScript, (ulong)sent, null, 0, out var spent);
            var mass = MassCalculator.Calculate(tx, spent);
            if (mass.FeeMass > MassCalculator.MaxStandardTransactionMass)
            {
                tooBig = true;
                failure = $"Transaction mass {mass.FeeMass} exceeds the standard limit {MassCalculator.MaxStandardTransactionMass} " +
                          $"with {chosen.Count} input(s).";
                return null;
            }

            var required = feeOverrideSompi is null
                ? checked((long)MassCalculator.EstimateFee(mass.FeeMass, feeRate))
                : fee;
            if (fee >= required)
                return new SweepPlan(tx, spent, mass, fee, (ulong)sent);
            fee = required;
        }

        failure = "Fee estimation did not converge.";
        return null;
    }
}
