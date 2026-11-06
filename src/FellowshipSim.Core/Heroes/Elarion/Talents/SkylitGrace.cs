using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>
/// Talent 200: "While Starfall Volley is active, your Skystrider's Grace gains +120% Cooldown Acceleration." Effect 2316
/// (AggregateBySource, StackCountMax 1) comes at each cast (GA_Bowguy_TargetedAoeDamage offset 448) and leaves when any
/// volley ends (BP_Bowguy_TargetedAoeDamage_AoeActor offsets 1998, 2062), with no test for another volley falling, so a
/// later volley still falling has no bonus until the next cast. The native stacking path is undecoded. The effect is not
/// in the combat logs (fXyzwGcQkTrLD34R fight 4 has the talent), so the engine's aura is internal.
/// </summary>
public class SkylitGrace : Talent
{
    public const int TalentId = 200;
    public const string TalentName = "Skylit Grace";
    public const double Acceleration = 1.2;

    /// <summary>Skystrider's Grace's cooldown reads it.</summary>
    public static readonly AuraDef Buff = AuraDef.Internal("Skylit Grace", 0);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<StarfallVolley>() is not { } starfall || hero.GetAbility<SkystridersGrace>() is not { } grace) return;
        starfall.VolleyBegan += ctx => hero.Auras.Apply(Buff, ctx.CurrentTime);
        starfall.VolleyEnded += ctx => hero.Auras.Remove(Buff, ctx.CurrentTime);
        hero.CooldownAccelerations.Add((ability, now) => ability == grace && hero.Auras.HasAura(Buff, now) ? Acceleration : 0.0);
        hero.CooldownAccelerationAuraKeys.Add(Buff.Key);
    }
}
