using FellowshipSim.Core.Heroes.Xavian.Abilities;

namespace FellowshipSim.Core.Heroes.Xavian.Talents;

/// <summary>
/// Talent 578: "Your Brilliant Flash deals 50% more damage. Each time you use an Offensive Ability, you have a 6% chance to
/// instantly reset the cooldown of Omnistrike and cause your next Omnistrike within 12 seconds to grant you 1 stack of
/// Omega Reprieval." Ability 1855 constant Talent.PowerScaler 1.5; effect 2661 "Golden Hour" (12s; its text gives +25%
/// crit chance, constant Talent.CooldownProc.Buff.CritChanceIncrease 0). Combat logs (XyGpcHjNaAzgk7mW fight 20 @ 29.724):
/// the Omnistrike removes Golden Hour and applies 1 stack of Omega Reprieval.
/// </summary>
public class GoldenHour : Talent
{
    public const int TalentId = 578;
    public const string TalentName = "Golden Hour";
    public const double FlashDamageMultiplier = 1.5;
    public const double Chance = 0.06;
    public const string ProcKey = "golden_hour";
    public const int BuffEffectId = 2661;
    public const double BuffSeconds = 12.0;
    public const int OmegaStacks = 1;

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, BuffEffectId, BuffSeconds);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var xavian = (Xavian)hero;
        var omnistrike = xavian.GetAbility<Omnistrike>()!;
        xavian.BrilliantFlash.AddDamageMultiplier(FlashDamageMultiplier);
        omnistrike.Struck += ctx =>
        {
            if (!hero.Auras.HasAura(Buff, ctx.CurrentTime)) return;
            hero.Auras.Remove(Buff, ctx.CurrentTime);
            hero.Auras.Apply(OmegaReprieval.Buff, ctx.CurrentTime, OmegaStacks);
        };
        xavian.OffensiveAbilityUsed += (ctx, _) =>
        {
            if (!ctx.Outcomes.RollProc(ProcKey, Chance, ctx)) return;
            hero.CooldownOf(omnistrike).ResetAll();
            hero.Auras.Apply(Buff, ctx.CurrentTime);
        };
    }
}
