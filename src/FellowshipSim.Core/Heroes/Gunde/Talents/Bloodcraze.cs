using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Core.Heroes.Gunde.Talents;

/// <summary>
/// Talent 761 / buff effect 3222, damage effect 3221: "Your Owed in Blood causes you to pulsate damage equal to a share of
/// Strength in a radius around you at a regular interval for a time. The damage of Bloodcraze is increased per stack of
/// Owed in Blood that was consumed. (Damage reduced beyond a target threshold)" The pulses come at
/// <see cref="Constants.Period"/> intervals after the cast (<see cref="Pulses"/> of them).
/// UNVERIFIED: no combat log has this talent.
/// </summary>
public partial class Bloodcraze : Talent
{
    public const int BuffEffectId = 3222;
    public const int DamageEffectId = 3221;
    public const int Pulses = 2;

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, BuffEffectId, TalentDuration);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var gunde = (Gunde)hero;
        var pulse = new Pulse(gunde);
        gunde.GetAbility<OwedInBlood>()!.Spent += (ctx, _, stacks) =>
        {
            hero.Auras.Apply(Buff, ctx.CurrentTime);
            double multiplier = 1.0 + Constants.DamageIncreasePerStack * stacks;
            for (int i = 1; i <= Pulses; i++)
            {
                ctx.Schedule(SimTime.FromSeconds(Constants.Period * i), c =>
                {
                    var targets = c.Outcomes.SelectTargets(pulse.DamageLogId, c.ActiveEnemies, c);
                    if (targets.Count > 0) pulse.DealAoeDamage(c, targets, damageMultiplier: multiplier);
                }, TalentName);
            }
        };
    }

    /// <summary>The pulse's damage: a share of Strength (<see cref="Constants.BaseStrengthCoefficient"/>), logged under effect 3221, falling off beyond <see cref="Constants.TargetThresholdScaling"/> targets.</summary>
    private sealed class Pulse : Ability
    {
        public Pulse(Gunde owner) : base(owner, LogIds.Effect(DamageEffectId), TalentName)
        {
            BaseCoefficient = Constants.BaseStrengthCoefficient;
            AoeTargetThreshold = Constants.TargetThresholdScaling;
        }

        public override void Execute(SimulationContext ctx, EnemyTarget target) { }
    }
}
