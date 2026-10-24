using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>
/// Talent 167: "Your Focused Shot has a 20% chance to grant you 1 charge of Empowered Multishot. All your Empowered
/// Multishots deal 20% more damage." Effect 2271 (15s, up to 2 charges); effect 2010 talent AoeProjectileDamageEmpowered
/// DamageBonus 1.2. Rolled as the arrow lands.
/// </summary>
public class FocusedExpanse : Talent
{
    public const int TalentId = 167;
    public const string TalentName = "Focused Expanse";
    public const double ProcChance = 0.2;
    public const double EmpoweredDamage = 1.2;
    public const int BuffEffectId = 2271;
    public const double BuffSeconds = 15.0;
    public const int MaxCharges = 2;
    public const string ProcKey = "focused_expanse";

    public static readonly AuraDef Buff = AuraDef.Effect("Focused Expanse", BuffEffectId, BuffSeconds, MaxCharges);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var multishot = hero.GetAbility<Multishot>();
        if (multishot == null) return;
        multishot.EmpoweringBuffs.Add(new Multishot.EmpoweringBuff(now => hero.Auras.HasAura(Buff, now), now => hero.Auras.ConsumeStack(Buff, now)));
        multishot.DamageMultipliers.Add((_, empowered) => empowered ? EmpoweredDamage : 1.0);
        hero.GetAbility<FocusedShot>()!.Landed += (ctx, _) =>
        {
            if (ctx.Outcomes.RollProc(ProcKey, ProcChance, ctx)) hero.Auras.Apply(Buff, ctx.CurrentTime);
        };
    }
}
