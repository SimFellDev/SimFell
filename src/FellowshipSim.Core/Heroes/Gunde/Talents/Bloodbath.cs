using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Core.Heroes.Gunde.Talents;

/// <summary>
/// Talent 764 / effect 3216 (3s): "Your Grim Carve grants you Bloodbath for 3 seconds, causing your Reign in Blood ability
/// to gain +200% Cooldown Acceleration for the duration." Combat logs: it comes as Grim Carve is cast.
/// </summary>
public class Bloodbath : Talent
{
    public const int TalentId = 764;
    public const string TalentName = "Bloodbath";
    public const int EffectId = 3216;
    public const double BuffSeconds = 3.0;
    public const double Acceleration = 2.0;

    // Combat logs: the buff stays through the hero's death (1 death).
    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, EffectId, BuffSeconds) with { KeepsOnDeath = true };

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability is GrimCarve) hero.Auras.Apply(Buff, now);
        };
        hero.CooldownAccelerations.Add((ability, now) => ability is ReignInBlood && hero.Auras.HasAura(Buff, now) ? Acceleration : 0.0);
        hero.CooldownAccelerationAuraKeys.Add(Buff.Key);
    }
}
