using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// A decorator to explicitly control which procs trigger during a test, and to read the chance of every proc roll. Installed by
/// <c>f.ForceProcs(key =&gt; key == Avalanche.ProcKey ? true : null)</c>.
///
/// Every proc in the engine (a talent's chance, Spirit Refund, bad luck protection...) is a <see cref="IOutcomeSource.RollProc"/> call with
/// a key naming the proc and its chance. For each call, <paramref name="forceProc"/> returns true or false to decide the proc, or null to
/// let the wrapped source roll it.
///
/// Every call is recorded in <see cref="Rolls"/> with its chance, forced or not. A test reads a proc's chance through <see
/// cref="ChancesOf"/> instead of rolling it many times, forcing the proc to false when only its chance matters.
///
/// Unlike the other decorators, a forced proc skips the wrapped source's roll, which shifts the random sequence for everything after it.
/// Tests force every outcome they read, so this does not affect them.
/// </summary>
public sealed class ForcedProcs(IOutcomeSource inner, Func<string, bool?> forceProc) : OutcomeSourceDecorator(inner)
{
    /// <summary>Every proc rolled so far, forced or not: its key and the chance the engine gave it.</summary>
    public List<(string Key, double Chance)> Rolls { get; } = new();

    /// <summary>The chance of each roll of the proc <paramref name="key"/> so far, in order. This is how a test checks a proc chance.</summary>
    public List<double> ChancesOf(string key) => Rolls.Where(r => r.Key == key).Select(r => r.Chance).ToList();

    public override bool RollProc(string procKey, double chance, SimulationContext ctx, EnemyTarget? target = null)
    {
        Rolls.Add((procKey, chance));
        return forceProc(procKey) ?? Inner.RollProc(procKey, chance, ctx, target);
    }
}
