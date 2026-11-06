using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Core.Heroes.Gunde.Talents;

/// <summary>
/// Talent 761 / buff effect 3222, damage effect 3221: "Your Owed in Blood causes you to pulsate damage equal to a share of
/// Strength in a radius around you at a regular interval for a time. The damage of Bloodcraze is increased per stack of
/// Owed in Blood that was consumed. (Damage reduced beyond a target threshold)" The pulses come at
/// <see cref="PulseSeconds"/> intervals after the cast (<see cref="Pulses"/> of them).
/// UNVERIFIED: no combat log has this talent.
/// </summary>
public class Bloodcraze : Talent
{
    public const int TalentId = 761;
    public const string TalentName = "Bloodcraze";
    public const int BuffEffectId = 3222;
    public const int DamageEffectId = 3221;
    public const double Coefficient = 0.25;
    public const double DamagePerStack = 1.5;
    public const double PulseSeconds = 3.0;
    public const int Pulses = 2;
    public const double BuffSeconds = 6.15;
    public const double TargetThreshold = 1;

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, BuffEffectId, BuffSeconds);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var gunde = (Gunde)hero;
        var pulse = new Pulse(gunde);
        gunde.GetAbility<OwedInBlood>()!.Spent += (ctx, _, stacks) =>
        {
            hero.Auras.Apply(Buff, ctx.CurrentTime);
            double multiplier = 1.0 + DamagePerStack * stacks;
            for (int i = 1; i <= Pulses; i++)
            {
                ctx.Schedule(SimTime.FromSeconds(PulseSeconds * i), c =>
                {
                    var targets = c.Outcomes.SelectTargets(pulse.DamageLogId, c.ActiveEnemies, c);
                    if (targets.Count > 0) pulse.DealAoeDamage(c, targets, damageMultiplier: multiplier);
                }, TalentName);
            }
        };
    }

    /// <summary>The pulse's damage: a share of Strength (<see cref="Coefficient"/>), logged under effect 3221, falling off beyond <see cref="TargetThreshold"/> targets.</summary>
    private sealed class Pulse : Ability
    {
        public Pulse(Gunde owner) : base(owner, LogIds.Effect(DamageEffectId), TalentName)
        {
            BaseCoefficient = Coefficient;
            AoeTargetThreshold = TargetThreshold;
        }

        public override void Execute(SimulationContext ctx, EnemyTarget target) { }
    }
}
