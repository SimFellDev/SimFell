using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Core.Heroes.Rime.Talents;

/// <summary>Talent 628: Frost Swallow hits have a chance to trigger a Bursting Ice pulse on the target.</summary>
public class BurstingSwallows : Talent
{
    public const int TalentId = 628;
    public const string TalentName = "Bursting Swallows";
    public const double ProcChance = 0.12;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public const string ProcKey = "bursting_swallows";

    /// <summary>Tooltip: Frost Swallow hits have a chance to trigger a pulse of Bursting Ice on the target.</summary>
    public override void Bind(Hero hero)
    {
        var burstingIce = hero.GetAbility<BurstingIce>();
        if (burstingIce == null) return;
        // Every Frost Swallow hit, Flight of the Navir's too.
        var swallows = new FrostSwallows?[] { hero.GetAbility<FrostSwallows>(), hero.GetAbility<FlightOfTheNavir>()?.Swallows };
        foreach (var s in swallows.OfType<FrostSwallows>())
        {
            s.Hit += (in hit) =>
            {
                var ctx = hit.SimContext;
                if (ctx == null || hit.Target == null) return;
                if (ctx.Outcomes.RollProc(ProcKey, ProcChance, ctx)) burstingIce.DealPulse(ctx, hit.Target);
            };
        }
    }
}
