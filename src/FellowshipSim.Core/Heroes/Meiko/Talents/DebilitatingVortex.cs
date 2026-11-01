using FellowshipSim.Core.Heroes.Meiko.Abilities;

namespace FellowshipSim.Core.Heroes.Meiko.Talents;

/// <summary>
/// Talent 97: "Enemies hit by Spirited Vortex have 20% reduced attack speed for 5 seconds." Effect 1511 on the enemy
/// (AttackSpeed x0.8; Meiko.DurationalAoeDamageBuff.Talent.ReducedAttackSpeed.Duration 5). Enemy attacks are not simulated.
/// </summary>
public class DebilitatingVortex : Talent
{
    public const int TalentId = 97;
    public const string TalentName = "Debilitating Vortex";
    public const int DebuffEffectId = 1511;
    public const double DebuffSeconds = 5.0;

    /// <summary>Effect 1511, named Harsh Winds in the game data.</summary>
    public static readonly AuraDef Debuff = AuraDef.Effect("Harsh Winds", DebuffEffectId, DebuffSeconds);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<SpiritedVortex>() is not { } vortex) return;
        vortex.Hit += (in hit) => hit.Target?.Auras.Apply(Debuff, hit.Timestamp);
    }
}
