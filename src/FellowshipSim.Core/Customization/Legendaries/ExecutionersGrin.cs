using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 423 (Tariq's Stormlord Wraps, item 5900; Executioner's Unsanitary Bands, item 5225): "Your Abilities
/// have a 6% chance to grant you Executioner's Grin for 8 seconds, causing your next Culling Strike to be useable on any
/// target regardless of their health, and have no cost, and deal damage as if it spent 30 Fury." Effect 2267 (8s, 2
/// stacks; its own text says 20 Fury). Rolled once per cast of any of Tariq's abilities.
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
    /// The legendary's "as if it spent 30 Fury". Combat logs (fXyzwGcQkTrLD34R fight 4): Culling Strikes with Executioner's
    /// Grin deal about 2.6 times those that spent 10 Fury, (1 + 0.4 x 30) / (1 + 0.4 x 10).
    /// </summary>
    public const double FurySpent = 30.0;
    public const string ProcKey = "executioners_grin";

    public static readonly AuraDef Buff = AuraDef.Effect("Executioner's Grin", BuffEffectId, BuffSeconds, MaxStacks);

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
            if (ability is not TariqAbility || hero.SimContext is not { } ctx) return;
            if (ctx.Outcomes.RollProc(ProcKey, ProcChance, ctx)) hero.Auras.Apply(Buff, now);
        };
    }
}
