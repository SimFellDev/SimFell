using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>
/// Talent 167: Focused Shot has a chance to grant a charge of Empowered Multishot. All Empowered
/// Multishots deal more damage. Effect 2271; effect 2010 talent AoeProjectileDamageEmpowered
/// DamageBonus. Combat logs (fXyzwGcQkTrLD34R fight 4): the charge comes as the cast
/// completes, before the arrow lands.
/// </summary>
public partial class FocusedExpanse : Talent
{
    public const int BuffEffectId = 2271;
    public const double BuffSeconds = 15.0;
    public const int MaxCharges = 2;
    public const string ProcKey = "focused_expanse";

    public static readonly AuraDef Buff = AuraDef.Effect("Focused Expanse", BuffEffectId, BuffSeconds, MaxCharges) with { IsProc = true };

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var multishot = hero.GetAbility<Multishot>();
        if (multishot == null) return;
        multishot.EmpoweringBuffs.Add(new Multishot.EmpoweringBuff(now => hero.Auras.HasAura(Buff, now), now => hero.Auras.ConsumeStack(Buff, now)));
        multishot.DamageMultipliers.Add((_, empowered) => empowered ? Constants.DamageBonus : 1.0);
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability is not FocusedShot || hero.SimContext is not { } ctx) return;
            if (ctx.Outcomes.RollProc(ProcKey, Constants.ProcChance, ctx)) hero.Auras.Apply(Buff, now);
        };
    }
}
