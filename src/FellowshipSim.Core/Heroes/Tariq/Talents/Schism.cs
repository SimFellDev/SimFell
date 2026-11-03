using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Heroes.Tariq.Talents;

/// <summary>
/// Talent 85: "Your Skull Crusher has a 35% chance to make your next Hammer Storm deal +150% increased damage. Your Hammer
/// Storm has a 35% chance to make your next Skull Crusher deal +150% increased damage. You can have up to 2 stacks of each
/// Schism at any one time." Effects 1746 (next Skull Crusher) and 1747 (next Hammer Storm), 2 stacks each. Rolled once per
/// cast, with bad luck protection (<see cref="BadLuckProtection"/>): a counter for each direction. Combat logs (17 fights,
/// 5265 rolls), log-likelihood: a counter for each -3246, one counter for both -3610, a flat 35% -3388. UNVERIFIED: the
/// step (game data has none), taken as the one whose average is the tooltip's 35%.
/// </summary>
public class Schism : Talent
{
    public const int TalentId = 85;
    public const string TalentName = "Schism";
    /// <summary>Tooltip text: "35% chance", the average of the bad luck protected chance.</summary>
    public const double ProcChance = 0.35;
    public static readonly double ProcStep = BadLuckProtection.StepFor(ProcChance);
    public const double DamageMultiplier = 2.5;
    public const int MaxStacks = 2;
    public const int SkullCrusherEffectId = 1746;
    public const int HammerStormEffectId = 1747;
    public const string SkullCrusherProcKey = "schism_skull_crusher";
    public const string HammerStormProcKey = "schism_hammer_storm";

    public static readonly AuraDef SkullCrusherBuff = AuraDef.Effect("Schism - Skull Crusher", SkullCrusherEffectId, maxStacks: MaxStacks) with { IsProc = true };
    public static readonly AuraDef HammerStormBuff = AuraDef.Effect("Schism - Hammer Storm", HammerStormEffectId, maxStacks: MaxStacks) with { IsProc = true };

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        // The spender uses up its own Schism as it resolves...
        hero.OnCastResolving += cast =>
        {
            var buff = cast.Ability switch
            {
                SkullCrusher => SkullCrusherBuff,
                HammerStorm => HammerStormBuff,
                _ => null,
            };
            if (buff == null || !hero.Auras.HasAura(buff, cast.Time)) return;
            hero.Auras.ConsumeStack(buff, cast.Time);
            cast.DamageMultiplier *= DamageMultiplier;
        };
        // ...and may make the other one's.
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (hero.SimContext is not { } ctx) return;
            if (ability is SkullCrusher && hero.RollBadLuckProc(ctx, HammerStormProcKey, ProcStep)) hero.Auras.Apply(HammerStormBuff, now);
            else if (ability is HammerStorm && hero.RollBadLuckProc(ctx, SkullCrusherProcKey, ProcStep)) hero.Auras.Apply(SkullCrusherBuff, now);
        };
    }
}
