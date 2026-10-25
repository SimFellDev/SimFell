using System.Globalization;
using System.Text;
using FellowshipSim.Engine;

namespace FellowshipSim.Cli;

/// <summary>A sim result as text: mean, lowest and highest fight DPS, standard deviation, fights, then each ability's DPS.</summary>
public static class TextSummary
{
    public static string Format(SimResult result)
    {
        var culture = CultureInfo.InvariantCulture;
        var rows = new List<(string Label, string Value)>
        {
            ("Mean DPS", result.MeanDps.ToString("N1", culture)),
            ("Min DPS", result.MinDps.ToString("N1", culture)),
            ("Max DPS", result.MaxDps.ToString("N1", culture)),
            ("Std dev", result.StandardDeviation.ToString("N1", culture)),
            ("Iterations", result.Iterations.ToString("N0", culture)),
            ("Duration", result.DurationSeconds.ToString("0.###", culture) + " s"),
        };
        var abilities = result.AbilityDps.OrderByDescending(a => a.Value)
            .Select(a => (Label: a.Key, Value: a.Value.ToString("N1", culture)))
            .ToList();
        var all = rows.Concat(abilities).ToList();
        int labelWidth = all.Max(r => r.Label.Length);
        int valueWidth = all.Max(r => r.Value.Length);

        var s = new StringBuilder();
        foreach (var (label, value) in rows) s.AppendLine($"{label.PadRight(labelWidth)}  {value.PadLeft(valueWidth)}");
        if (abilities.Count > 0)
        {
            s.AppendLine();
            s.AppendLine("DPS by ability");
            foreach (var (label, value) in abilities) s.AppendLine($"{label.PadRight(labelWidth)}  {value.PadLeft(valueWidth)}");
        }
        return s.ToString();
    }
}
