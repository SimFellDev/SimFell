namespace FellowshipSim.Core.Heroes.Gunde.Abilities;

/// <summary>
/// Ability 2295 / effect 3241: "For a time, your abilities apply an additional share of the damage dealt to
/// Rend." Combat logs (Gunde guide corpus, 69 logs): it takes no global cooldown.
/// </summary>
public class ReignInBlood : GundeAbility
{
    public const int AbilityId = 2295;
    public const string AbilityName = "Reign in Blood";
    public const double CooldownSeconds = 90.0;
    public const int BuffEffectId = 3241;
    public const double BuffSeconds = 12.0;
    public const double RendShareBonus = 0.3;

    public static readonly AuraDef Buff = AuraDef.Effect("Reign in Blood", BuffEffectId, BuffSeconds);

    /// <summary>Share added to Rend while Reign in Blood holds (Frenzied Reign adds to it).</summary>
    public double ShareBonus { get; set; } = RendShareBonus;

    public ReignInBlood(Gunde owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Major)
    {
        SetCooldown(CooldownSeconds);
        IgnoresGcd = true;
        owner.RendShareBonuses.Add(now => owner.Auras.HasAura(Buff, now) ? ShareBonus : 0.0);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        Gunde.Auras.Apply(Buff, ctx.CurrentTime);
    }
}
