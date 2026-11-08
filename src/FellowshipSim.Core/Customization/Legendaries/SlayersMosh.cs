using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 424: "Enemies that take damage from your Leap Smash ability take more damage from you." Effect 2250 on
/// the enemy.
/// </summary>
public partial class SlayersMosh : Legendary
{
    public const int DebuffEffectId = 2250;

    public static readonly AuraDef Debuff = AuraDef.Effect("Slayer's Mosh", DebuffEffectId, Duration);

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<LeapSmash>() is not { } leapSmash) return;
        leapSmash.Smashed += (ctx, struck) =>
        {
            for (int i = 0; i < struck.Count; i++) struck[i].Auras.Apply(Debuff, ctx.CurrentTime);
        };
        hero.TargetDamageMultipliers.Add((target, now) => target.Auras.HasAura(Debuff, now) ? IncomingDamageScaler : 1.0);
    }
}
