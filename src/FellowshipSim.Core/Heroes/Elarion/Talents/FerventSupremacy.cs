using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>
/// Talent 136: Skystrider's Supremacy's cooldown is reduced and it grants charges of Empowered Multishot that last longer.
/// While Fervent Supremacy is active, Multishot deals more damage. Effect 2009
/// talent ChargesCooldownAndDuration, effect 2010 DamageBonus.
/// Combat logs (fXyzwGcQkTrLD34R fight 4, pWmcV42K39GHTLQX fight 23): the cast applies effect 2009 at its charges as stacks, and each
/// Multishot takes one.
/// </summary>
public partial class FerventSupremacy : Talent
{
    public const double BuffSeconds = 15.0;

    /// <summary>Skystrider's Supremacy's buff as Fervent Supremacy makes it: <see cref="MaxStacks"/> charges for <see cref="BuffSeconds"/>.</summary>
    public static readonly AuraDef Buff = SkystridersSupremacy.Buff.WithDuration(BuffSeconds);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<SkystridersSupremacy>() is not { } supremacy) return;
        supremacy.ShortenCooldown(ChargesCooldownAndDurationCooldownReductionInSeconds);
        supremacy.CastBuff = Buff;
        supremacy.ChargesGranted = MaxStacks;
        hero.GetAbility<Multishot>()?.DamageMultipliers.Add((now, _) => hero.Auras.HasAura(Buff, now) ? ChargesCooldownAndDurationDamageBonus : 1.0);
    }
}
