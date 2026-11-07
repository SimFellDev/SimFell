using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 423 (Tariq's Stormlord Wraps, item 5900; Executioner's Unsanitary Bands, item 5225): "Your Abilities
/// have a chance to grant you Executioner's Grin, causing your next Culling Strike to be useable on any
/// target regardless of their health, and have no cost, and deal damage as if it spent extra Fury." Effect 2267 (its own
/// text names a different Fury amount). Rolled once per cast of any ability, the weapon's too (combat logs, fXyzwGcQkTrLD34R
/// fight 4 @ 66.7: a Fated Strike grants it).
/// </summary>
public class ExecutionersGrin : Legendary
{
    public const int PowerId = 423;
    public const int WrapsItemId = 5900;
    public const int BandsItemId = 5225;
    public const int BuffEffectId = 2267;
    public const double BuffSeconds = 8.0;
    public const int MaxStacks = 2;
    public const double ProcChance = 0.06;
    /// <summary>
    /// The Fury the legendary's Culling Strike deals damage as if it spent. Combat logs (fXyzwGcQkTrLD34R fight 4): Culling
    /// Strikes with Executioner's Grin deal damage as the Culling Strike formula gives at this Fury.
    /// </summary>
    public const double FurySpent = 30.0;
    public const string ProcKey = "executioners_grin";

    public static readonly AuraDef Buff = AuraDef.Effect("Executioner's Grin", BuffEffectId, BuffSeconds, MaxStacks) with { IsProc = true };

    public override int LegendaryPowerId => PowerId;
    public override string Name => "Executioner's Grin";
    public override string HeroName => "Tariq";
    public override IReadOnlyList<int> ItemIds => [WrapsItemId, BandsItemId];

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<CullingStrike>() is not { } cullingStrike) return;
        cullingStrike.FreeStrike = Buff;
        cullingStrike.FreeStrikeFury = FurySpent;
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (hero.SimContext is not { } ctx) return;
            if (ctx.Outcomes.RollProc(ProcKey, ProcChance, ctx)) hero.Auras.Apply(Buff, now);
        };
    }
}
