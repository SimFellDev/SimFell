using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Core.Heroes.Gunde.Talents;

/// <summary>
/// Talent 764 / effect 3216: "Your Grim Carve grants you Bloodbath for a time, causing your Reign in Blood ability
/// to gain Cooldown Acceleration for the duration." Combat logs: it comes as Grim Carve is cast.
/// </summary>
public partial class Bloodbath : Talent
{
    public const int EffectId = 3216;

    // Combat logs: the buff stays through the hero's death (1 death).
    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, EffectId, Constants.CooldownAccelerationDuration) with { KeepsOnDeath = true };

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability is GrimCarve) hero.Auras.Apply(Buff, now);
        };
        hero.CooldownAccelerations.Add((ability, now) => ability is ReignInBlood && hero.Auras.HasAura(Buff, now) ? Constants.TargetedAoeProjectileTalentCooldownAccelerationAdditionalCooldownRecovery : 0.0);
        hero.CooldownAccelerationAuraKeys.Add(Buff.Key);
    }
}
