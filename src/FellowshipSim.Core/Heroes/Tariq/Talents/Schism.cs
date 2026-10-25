using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Heroes.Tariq.Talents;

/// <summary>
/// Talent 85: "Your Skull Crusher has a 35% chance to make your next Hammer Storm deal +150% increased damage. Your Hammer
/// Storm has a 35% chance to make your next Skull Crusher deal +150% increased damage. You can have up to 2 stacks of each
/// Schism at any one time." Effects 1746 (next Skull Crusher) and 1747 (next Hammer Storm), 2 stacks each. Rolled once per
/// cast.
/// </summary>
public class Schism : Talent
{
    public const int TalentId = 85;
    public const string TalentName = "Schism";
    public const double ProcChance = 0.35;
    public const double DamageMultiplier = 2.5;
    public const int MaxStacks = 2;
    public const int SkullCrusherEffectId = 1746;
    public const int HammerStormEffectId = 1747;
    public const string SkullCrusherProcKey = "schism_skull_crusher";
    public const string HammerStormProcKey = "schism_hammer_storm";

    public static readonly AuraDef SkullCrusherBuff = AuraDef.Effect("Schism - Skull Crusher", SkullCrusherEffectId, maxStacks: MaxStacks);
    public static readonly AuraDef HammerStormBuff = AuraDef.Effect("Schism - Hammer Storm", HammerStormEffectId, maxStacks: MaxStacks);

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
            if (ability is SkullCrusher && ctx.Outcomes.RollProc(HammerStormProcKey, ProcChance, ctx)) hero.Auras.Apply(HammerStormBuff, now);
            else if (ability is HammerStorm && ctx.Outcomes.RollProc(SkullCrusherProcKey, ProcChance, ctx)) hero.Auras.Apply(SkullCrusherBuff, now);
        };
    }
}
