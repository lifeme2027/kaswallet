using System.Globalization;

namespace KasWallet.Core.Consensus;

/// <summary>Human-readable <c>key=value</c> lines describing a built transaction's mass and fee.</summary>
public static class FeeReport
{
    public static IReadOnlyList<string> Lines(BuiltTransaction built)
    {
        if (built is null)
            throw new ArgumentNullException(nameof(built));

        var m = built.Mass;
        var kas = (built.FeeSompi / 100_000_000m).ToString("0.00000000", CultureInfo.InvariantCulture);
        var lines = new List<string>
        {
            $"mass={m.FeeMass}",
            $"mass_compute={m.ComputeMass}",
            $"mass_transient={m.NormalizedTransientMass}",
            $"mass_storage={m.NormalizedStorageMass}",
            $"fee_rate={built.FeeRate}",
            $"fee_sompi={built.FeeSompi}",
            $"fee_kas={kas}",
            $"change_sompi={built.ChangeSompi}"
        };
        var required = checked(m.FeeMass * built.FeeRate);
        if (built.FeeSompi < required)
            lines.Add($"warning=fee {built.FeeSompi} sompi is below mass*fee-rate ({required}); nodes will likely reject it");
        return lines;
    }
}
