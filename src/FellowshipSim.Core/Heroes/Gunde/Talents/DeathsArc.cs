using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Core.Heroes.Gunde.Talents;

/// <summary>
/// Talent 748 / effect 3208: "Each time you use Blood Arc, it has a chance to instantly reset its cooldown and
/// cause your next Blood Arc to have increased Critical Strike chance." Crit chance beyond a guaranteed crit is a grievous crit.
/// </summary>
public partial class DeathsArc : Talent
{
    public const int EffectId = 3208;
    public const string ProcKey = "deaths_arc";

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, EffectId, Duration) with { IsProc = true };

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
            cast.CritChance += AddedCriticalStrikeChance;
        };
        bloodArc.Arced += (ctx, _) =>
        {
            if (!ctx.Outcomes.RollProc(ProcKey, Chance, ctx)) return;
            hero.CooldownOf(bloodArc).ResetAll();
            hero.Auras.Apply(Buff, ctx.CurrentTime);
        };
    }
}
