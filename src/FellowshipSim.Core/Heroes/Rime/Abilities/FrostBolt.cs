using FellowshipSim.Core.Heroes.Rime.Talents;

namespace FellowshipSim.Core.Heroes.Rime.Abilities;

/// <summary>Ability 1029. Generates Anima.</summary>
public partial class FrostBolt : RimeAbility
{
    /// <summary>The bolt hit an enemy (after its travel), with the damage it dealt (Burstbolter).</summary>
    public event Action<SimulationContext, EnemyTarget, double>? Landed;

    public FrostBolt(Rime owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = Constants.IntellectCoefficient;
        CastTime = SimTime.FromSeconds(AbilityCastTime);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        Rime.AddAnima(Constants.ResourceGain, ctx, target);
        DealDamage(ctx, target, onLanded: (c, dealt) => Landed?.Invoke(c, target, dealt));
    }
}
