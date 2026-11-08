namespace FellowshipSim.Core.Heroes.Gunde.Abilities;

/// <summary>
/// Ability 2295 / effect 3241: "For a time, your abilities apply an additional share of the damage dealt to
/// Rend." Combat logs (Gunde guide corpus, 69 logs): it takes no global cooldown.
/// </summary>
public partial class ReignInBlood : GundeAbility
{
    public const int BuffEffectId = 3241;

    public static readonly AuraDef Buff = AuraDef.Effect("Reign in Blood", BuffEffectId, AbilityDuration);

    /// <summary>Share added to Rend while Reign in Blood holds (Frenzied Reign adds to it).</summary>
    public double ShareBonus { get; set; } = BonusDotDamage;

    public ReignInBlood(Gunde owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        SetCooldown(AbilityCooldown);
        IgnoresGcd = true;
        owner.RendShareBonuses.Add(now => owner.Auras.HasAura(Buff, now) ? ShareBonus : 0.0);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        Gunde.Auras.Apply(Buff, ctx.CurrentTime);
    }
}
