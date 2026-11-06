using FellowshipSim.Core.Heroes.Rime.Talents;

namespace FellowshipSim.Core.Heroes.Rime.Abilities;

/// <summary>Ability 1029. Generates Anima.</summary>
public class FrostBolt : RimeAbility
{
    public const int AbilityId = 1029;
    public const string AbilityName = "Frost Bolt";
    public const double Coefficient = 2.017;
    public const double CastTimeSeconds = 1.5;
    public const double AnimaGain = 3.0;

    /// <summary>The bolt hit an enemy (after its travel), with the damage it dealt (Burstbolter).</summary>
    public event Action<SimulationContext, EnemyTarget, double>? Landed;

    public FrostBolt(Rime owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Basic)
    {
        BaseCoefficient = Coefficient;
        CastTime = SimTime.FromSeconds(CastTimeSeconds);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        Rime.AddAnima(AnimaGain, ctx, target);
        DealDamage(ctx, target, onLanded: (c, dealt) => Landed?.Invoke(c, target, dealt));
    }
}
