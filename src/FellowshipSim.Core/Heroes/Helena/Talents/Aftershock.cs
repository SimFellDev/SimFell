using FellowshipSim.Core.Heroes.Helena.Abilities;

namespace FellowshipSim.Core.Heroes.Helena.Talents;

/// <summary>
/// Talent 213: "Enemies that take damage from your Shockwave ability take 30% more damage from you for 5 seconds." The debuff
/// is effect 2428. Its game data is IncomingDamageMultiplier x1.2 (MultiplyCompound), not the tooltip's 30%:
/// Warmaster.MoveToTargetStun.Talent.ArrivalMeleeAoeSlow.TargetExtraWarmasterDamage.IncomingDamageScaler 1.2, owner effect
/// 2428 (entities.jsonl v26320967). Combat logs (8 fights with the talent): Helena's hits on an enemy with the debuff
/// deal 1.19-1.23 times what they deal without it.
/// </summary>
public class Aftershock : Talent
{
    public const int TalentId = 213;
    public const string TalentName = "Aftershock";
    public const int DebuffEffectId = 2428;
    public const double DebuffSeconds = 5.0;
    public const double DamageTaken = 1.2;

    public static readonly AuraDef Debuff = AuraDef.Effect(TalentName, DebuffEffectId, DebuffSeconds);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<Shockwave>() is not { } shockwave) return;
        shockwave.Struck += (ctx, enemy) => enemy.Auras.Apply(Debuff, ctx.CurrentTime);
        hero.TargetDamageMultipliers.Add((target, now) => target.Auras.HasAura(Debuff, now) ? DamageTaken : 1.0);
    }
}
