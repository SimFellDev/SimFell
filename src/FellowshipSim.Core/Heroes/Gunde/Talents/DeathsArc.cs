using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Core.Heroes.Gunde.Talents;

/// <summary>
/// Talent 748 / effect 3208 (12s): "Each time you use Blood Arc, it has a 30% chance to instantly reset its cooldown and
/// cause your next Blood Arc to have +100% Critical Strike chance." Overflow beyond 100% is a grievous crit.
/// </summary>
public class DeathsArc : Talent
{
    public const int TalentId = 748;
    public const string TalentName = "Death's Arc";
    public const int EffectId = 3208;
    public const double BuffSeconds = 12.0;
    public const double ProcChance = 0.3;
    public const double CritChance = 1.0;
    public const string ProcKey = "deaths_arc";

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, EffectId, BuffSeconds) with { IsProc = true };

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var bloodArc = hero.GetAbility<BloodArc>();
        if (bloodArc == null) return;
        // The Blood Arc cast with it crits; then each cast may reset the cooldown and grant the next one.
        hero.OnCastResolving += cast =>
        {
            if (cast.Ability != bloodArc || !hero.Auras.HasAura(Buff, cast.Time)) return;
            hero.Auras.Remove(Buff, cast.Time);
            cast.CritChance += CritChance;
        };
        bloodArc.Arced += (ctx, _) =>
        {
            if (!ctx.Outcomes.RollProc(ProcKey, ProcChance, ctx)) return;
            hero.CooldownOf(bloodArc).ResetAll();
            hero.Auras.Apply(Buff, ctx.CurrentTime);
        };
    }
}
