using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 423: "Your Abilities have a chance to grant you Executioner's Grin, causing your next Culling Strike to
/// be useable on any target regardless of their health, and have no cost, and deal damage as if it spent extra Fury."
/// Effect 2267 (its own text names a different Fury amount). Rolled once per cast of any ability, the weapon's too (combat
/// logs, fXyzwGcQkTrLD34R fight 4 @ 66.7: a Fated Strike grants it).
/// </summary>
public partial class ExecutionersGrin : Legendary
{
    public const int BuffEffectId = 2267;
    public const int MaxStacks = 2;
    /// <summary>
    /// The Fury the legendary's Culling Strike deals damage as if it spent. Combat logs (fXyzwGcQkTrLD34R fight 4): Culling
    /// Strikes with Executioner's Grin deal damage as the Culling Strike formula gives at this Fury.
    /// </summary>
    public const double FurySpent = 30.0;
    public const string ProcKey = "executioners_grin";

    public static readonly AuraDef Buff = AuraDef.Effect("Executioner's Grin", BuffEffectId, BuffDuration, MaxStacks) with { IsProc = true };

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<CullingStrike>() is not { } cullingStrike) return;
        cullingStrike.FreeStrike = Buff;
        cullingStrike.FreeStrikeFury = FurySpent;
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (hero.SimContext is not { } ctx) return;
            if (ctx.Outcomes.RollProc(ProcKey, Chance, ctx)) hero.Auras.Apply(Buff, now);
        };
    }
}
