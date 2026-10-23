using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>
/// Talent 162: "Your active Engulfing Flames and Searing Blaze durations are extended each time Fire Ball burns enemies
/// up to a total of 3 seconds over its full duration." Each Fire Ball tick extends the enemy's Engulfing Flames and
/// Searing Blaze by its share of the 3 seconds: 0.5s per tick of its 6. Combat logs (yMAZVJL9hTpFjqbC fight 7, 25.1 @
/// 25.88-41.38s): an Engulfing Flames of 12s lasts 15.5s through 7 Fire Ball ticks, some in Wildfire: 0.5s each at any tick
/// rate.
/// </summary>
public class SlowBurn : Talent
{
    public const int TalentId = 162;
    public const string TalentName = "Slow Burn";
    public const double ExtensionOverDurationSeconds = 3.0;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var ardeos = (Ardeos)hero;
        var fireBall = ardeos.GetAbility<FireBall>();
        var engulfing = ardeos.GetAbility<EngulfingFlames>()?.Dot;
        var searing = ardeos.GetAbility<SearingBlaze>()?.Dot;
        if (fireBall == null) return;
        var burn = fireBall.Dot;
        double perTick = ExtensionOverDurationSeconds * burn.BasePeriod / burn.Duration;
        burn.Ticked += (ctx, dot, fraction) =>
        {
            double seconds = perTick * fraction;
            foreach (var other in ardeos.Dots.On(dot.Target).Where(d => d.Effect == engulfing || d.Effect == searing).ToList())
            {
                ardeos.Dots.Extend(ctx, other, seconds);
            }
        };
    }
}
