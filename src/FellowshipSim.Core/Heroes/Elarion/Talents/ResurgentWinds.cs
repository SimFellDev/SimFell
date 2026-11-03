using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>
/// Talent 126: "Your Offensive Abilities have a 5% chance to grant you Resurgent Winds. Your Lunarlight Mark ability has a
/// 100% chance to grant you Resurgent Winds. When you gain Resurgent Winds, you instantly gain 1 charge of Highwind Arrow
/// and your next Highwind Arrow is Instant Cast, costs no Focus, and deals 50% more damage. Resurgent Winds lasts for 15
/// seconds and stacks up to 2 times." Effect 2074 (15s, 2 stacks). UNVERIFIED: which casts count as Offensive Abilities
/// (the engine rolls on every cast of a damaging ability but Shoot).
/// </summary>
public class ResurgentWinds : Talent
{
    public const int TalentId = 126;
    public const string TalentName = "Resurgent Winds";
    public const double ProcChance = 0.05;
    public const int BuffEffectId = 2074;
    public const double BuffSeconds = 15.0;
    public const int MaxStacks = 2;
    public const string ProcKey = "resurgent_winds";

    public static readonly AuraDef Buff = AuraDef.Effect("Resurgent Winds", BuffEffectId, BuffSeconds, MaxStacks) with { IsProc = true };

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
            if (ctx.Outcomes.RollProc(ProcKey, ProcChance, ctx)) Grant(ctx);
        };
    }

    private static bool IsOffensive(Ability ability) =>
        ability is FocusedShot or CelestialShot or Multishot or HighwindArrow or HeartseekerBarrage or StarfallVolley;
}
