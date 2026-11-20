using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>
/// Talent 162: active Engulfing Flames and Searing Blaze durations are extended each time Fire Ball burns enemies, up to a
/// total over its full duration. Each Fire Ball tick extends the enemy's Engulfing Flames and Searing Blaze by its share of
/// that total. Combat logs (yMAZVJL9hTpFjqbC fight 7, 25.1 @ 25.88-41.38s): an Engulfing Flames lasts longer through Fire
/// Ball ticks, some in Wildfire, and each tick extends it by the same share at any tick rate, a partial tick included. An
/// extension never leaves an effect more than its full duration (<see cref="DotInstance.FullDurationSeconds"/>), as
/// Incinerate's and Backdraft's.
/// </summary>
public partial class SlowBurn : Talent
{
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
            double seconds = perTick;
            // By index and without a copy: extending an effect leaves the enemy's list as it is.
            var on = ardeos.Dots.On(dot.Target);
            for (int i = 0; i < on.Count; i++)
            {
                var other = on[i];
                if (other.Effect == engulfing || other.Effect == searing) ardeos.Dots.Extend(ctx, other, seconds, maxRemainingSeconds: other.FullDurationSeconds);
            }
        };
    }
}
