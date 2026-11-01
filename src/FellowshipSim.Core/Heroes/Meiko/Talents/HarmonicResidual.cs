using FellowshipSim.Core.Heroes.Meiko.Abilities;

namespace FellowshipSim.Core.Heroes.Meiko.Talents;

/// <summary>
/// Talent 106: "While your Twin Souls: Bulwark is on cooldown your Stone Shield is reinforced, causing it to diminish by
/// 85% of the normal value when soaking damage" (Meiko.TauntTotem.Talent.DamageReductionWhileOnCooldown
/// .StoneShieldReductionScaler 0.85). Effect 3138 is on Meiko while the cooldown runs: combat logs (7Zmfx9gq6HjvThzc fight
/// 2 @ 14.326) apply it with the Bulwark cast. The stones' health is not simulated.
/// </summary>
public class HarmonicResidual : Talent
{
    public const int TalentId = 106;
    public const string TalentName = "Harmonic Residual";
    public const int BuffEffectId = 3138;
    public const double StoneShieldDrain = 0.85;

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, BuffEffectId);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<TwinSoulsBulwark>() is not { } bulwark) return;
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability != bulwark || hero.SimContext is not { } ctx) return;
            hero.Auras.Apply(Buff, now);
            EndWithCooldown(ctx, hero, bulwark);
        };
    }

    /// <summary>The buff ends with the cooldown (which Earthbourne can shorten).</summary>
    private static void EndWithCooldown(SimulationContext ctx, Hero hero, TwinSoulsBulwark bulwark)
    {
        double remains = hero.GetCooldownRemains(bulwark.Name, ctx.CurrentTime);
        if (remains <= 0)
        {
            hero.Auras.Remove(Buff, ctx.CurrentTime);
            return;
        }
        ctx.Schedule(SimTime.FromSeconds(remains), c => EndWithCooldown(c, hero, bulwark), TalentName);
    }
}
