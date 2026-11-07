using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>
/// Talent 157 / effect 2301: Detonate casts have a chance (a PPM rate) to grant a Fire Ball charge and give the next Fire
/// Ball critical strike chance. Combat logs (1p7AXmHfwV9CYMxR fight 30, dBgbn8wD1RxL7Kma fight 9): the buff counts match
/// the expected counts at the PPM rate over the Detonates cast. UNVERIFIED: the Fire Ball charge.
/// </summary>
public class ReignOfFire : Talent
{
    public const int TalentId = 157;
    public const string TalentName = "Reign of Fire";
    public const int EffectId = 2301;
    public const double BuffSeconds = 12.0;
    public const int MaxStacks = 2;
    public const double Ppm = 1.5;
    public const double CritChance = 2.0;
    public const string ProcKey = "reign_of_fire";

    public static readonly AuraDef Buff = AuraDef.Effect("Reign of Fire", EffectId, BuffSeconds, MaxStacks) with { IsProc = true };

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var fireBall = hero.GetAbility<FireBall>();
        if (fireBall == null) return;
        var rppm = new RealPpm(ProcKey, Ppm);
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability is not Detonate || hero.SimContext is not { } ctx || !rppm.Roll(hero, ctx)) return;
            hero.CooldownOf(fireBall).GrantCharge(now);
            hero.Auras.Apply(Buff, now);
        };
        hero.OnCastResolving += cast =>
        {
            if (cast.Ability != fireBall || !hero.Auras.HasAura(Buff, cast.Time)) return;
            hero.Auras.ConsumeStack(Buff, cast.Time);
            cast.CritChance += CritChance;
        };
    }
}
