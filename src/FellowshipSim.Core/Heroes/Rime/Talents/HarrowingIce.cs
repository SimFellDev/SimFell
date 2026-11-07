using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Core.Heroes.Rime.Talents;

/// <summary>
/// Talent 627 / effect 2839: each Bursting Ice pulse grants a stack; Bursting Ice deals more damage per
/// stack; the stack after the next-to-last consumes them all and reduces Flight of the Navir's cooldown.
/// Values: talent text and game data.
/// Combat logs (g6zBdpvJCmh2HfrD fight 31, 1056 pulses): a pulse deals damage with the stacks held before it, then adds
/// its stack; stacks reach the maximum and are removed at once.
/// </summary>
public class HarrowingIce : Talent
{
    public const int TalentId = 627;
    public const string TalentName = "Harrowing Ice";
    public const int EffectId = 2839;
    public const double BuffSeconds = 12.0;
    public const int MaxStacks = 31;
    public const double DamagePerStack = 0.03;
    public const double FlightOfTheNavirReductionSeconds = 5.0;

    public static readonly AuraDef Buff = AuraDef.Effect("Harrowing Ice", EffectId, BuffSeconds, MaxStacks);

    public override string Name => TalentName;
    public override int Id => TalentId;

    /// <summary>
    /// A pulse deals damage with the stacks held before it, then adds its own stack; the stack after the next-to-last consumes them all
    /// and shortens Flight of the Navir.
    /// </summary>
    public override void Bind(Hero hero)
    {
        var burstingIce = hero.GetAbility<BurstingIce>();
        if (burstingIce == null) return;
        var flightOfTheNavir = hero.GetAbility<FlightOfTheNavir>();
        burstingIce.PulseDamageMultipliers.Add(ctx => 1.0 + DamagePerStack * hero.Auras.GetStacks(Buff, ctx.CurrentTime));
        burstingIce.Pulsed += ctx =>
        {
            var now = ctx.CurrentTime;
            hero.Auras.Apply(Buff, now);
            if (hero.Auras.GetStacks(Buff, now) >= Buff.MaxStacks)
            {
                hero.Auras.Remove(Buff, now);
                if (flightOfTheNavir != null) hero.ReduceCooldown(flightOfTheNavir.Name, SimTime.FromSeconds(FlightOfTheNavirReductionSeconds), now);
            }
        };
    }
}
