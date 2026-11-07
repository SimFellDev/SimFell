using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>
/// Talent 200: While Starfall Volley is active, Skystrider's Grace gains Cooldown Acceleration. Each volley falling gives
/// the full bonus, and they add up (per the user, confirmed in game). Effect 2316 (AggregateBySource, StackCountMax)
/// reads as one merged effect that any volley's end removes; the user judges that reading of the game data wrong.
/// The effect is not in the combat logs (fXyzwGcQkTrLD34R fight 4 has the talent), so the engine's aura is internal.
/// </summary>
public class SkylitGrace : Talent
{
    public const int TalentId = 200;
    public const string TalentName = "Skylit Grace";
    public const double Acceleration = 1.2;

    /// <summary>One stack for each Starfall Volley falling; Skystrider's Grace's cooldown reads it.</summary>
    public static readonly AuraDef Buff = AuraDef.Internal("Skylit Grace", 0, maxStacks: 99);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<StarfallVolley>() is not { } starfall || hero.GetAbility<SkystridersGrace>() is not { } grace) return;
        starfall.VolleyBegan += ctx => VolleysChanged(hero, starfall, ctx);
        starfall.VolleyEnded += ctx => VolleysChanged(hero, starfall, ctx);
        // Each volley gives the full bonus, and they add up.
        hero.CooldownAccelerations.Add((ability, now) => ability == grace ? Acceleration * hero.Auras.GetStacks(Buff, now) : 0.0);
        hero.CooldownAccelerationAuraKeys.Add(Buff.Key);
    }

    private static void VolleysChanged(Hero hero, StarfallVolley starfall, SimulationContext ctx)
    {
        if (starfall.ActiveCount > 0) hero.Auras.Apply(Buff, ctx.CurrentTime, setStacks: starfall.ActiveCount);
        else hero.Auras.Remove(Buff, ctx.CurrentTime);
    }
}
