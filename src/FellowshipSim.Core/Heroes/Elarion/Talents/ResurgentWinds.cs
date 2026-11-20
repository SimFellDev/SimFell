using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>
/// Talent 126: Offensive Abilities have a chance to grant Resurgent Winds. Lunarlight Mark always grants Resurgent Winds.
/// Gaining Resurgent Winds grants a charge of Highwind Arrow, and the next Highwind Arrow is Instant Cast, costs no Focus,
/// and deals more damage. Resurgent Winds is timed and stacks. Effect 2074. UNVERIFIED: which casts count as Offensive Abilities
/// (the engine rolls on every cast of a damaging ability but Shoot).
/// </summary>
public partial class ResurgentWinds : Talent
{
    public const int BuffEffectId = 2074;
    public const int MaxStacks = 2;
    public const string ProcKey = "resurgent_winds";

    public static readonly AuraDef Buff = AuraDef.Effect("Resurgent Winds", BuffEffectId, Constants.InstantNoCooldownProcDuration, MaxStacks) with { IsProc = true };

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var highwind = hero.GetAbility<HighwindArrow>();
        if (highwind == null) return;
        highwind.HasResurgentWinds = true;

        void Grant(SimulationContext ctx)
        {
            hero.Auras.Apply(Buff, ctx.CurrentTime);
            hero.CooldownOf(highwind).GrantCharge(ctx.CurrentTime);
        }

        hero.GetAbility<LunarlightMark>()!.Cast += Grant;
        hero.OnCastCompleted += (ability, _, _) =>
        {
            if (hero.SimContext is not { } ctx || !IsOffensive(ability)) return;
            // The Offensive Ability proc chance (InstantNoCooldownProc.OffensiveAbility.ProcChance).
            if (ctx.Outcomes.RollProc(ProcKey, Constants.ProcChance, ctx)) Grant(ctx);
        };
    }

    private static bool IsOffensive(Ability ability) =>
        ability is FocusedShot or CelestialShot or Multishot or HighwindArrow or HeartseekerBarrage or StarfallVolley;
}
