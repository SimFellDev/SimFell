using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>
/// Talent 168: "When your Celestial Shot consumes Celestial Impetus the cooldown of your Heartseeker Barrage is reset and
/// you gain Impending Heartseeker. IMPENDING HEARTSEEKER: Your Heartseeker Barrage deals increased damage with each
/// arrow during its channel." Effect 2317, which Starstriker's Ascent grants too (<see cref="Grant"/>).
/// UNVERIFIED: no combat log has this talent.
/// </summary>
public partial class ImpendingHeartseeker : Talent
{
    public const int BuffEffectId = 2317;

    public static readonly AuraDef Buff = AuraDef.Effect("Impending Heartseeker", BuffEffectId, Constants.ResetCooldownAndBuffBuffDuration) with { IsProc = true };

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<CelestialShot>() is not { } celestialShot) return;
        celestialShot.ImpetusConsumed += (ctx, _) => Grant(hero, ctx);
    }

    /// <summary>Resets Heartseeker Barrage's cooldown and grants Impending Heartseeker, which the next barrage uses up.</summary>
    public static void Grant(Hero hero, SimulationContext ctx)
    {
        if (hero.GetAbility<HeartseekerBarrage>() is not { } barrage) return;
        hero.CooldownOf(barrage).ResetAll();
        hero.Auras.Apply(Buff, ctx.CurrentTime);
    }
}
