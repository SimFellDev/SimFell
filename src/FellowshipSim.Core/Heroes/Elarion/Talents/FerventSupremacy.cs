using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>
/// Talent 136: Skystrider's Supremacy's cooldown is reduced and it grants charges of Empowered Multishot that last longer.
/// While Fervent Supremacy is active, Multishot deals more damage. Effect 2009
/// talent ChargesCooldownAndDuration, effect 2010 DamageBonus.
/// Combat logs (fXyzwGcQkTrLD34R fight 4, pWmcV42K39GHTLQX fight 23): the cast applies effect 2009 at its charges as stacks, and each
/// Multishot takes one.
/// </summary>
public class FerventSupremacy : Talent
{
    public const int TalentId = 136;
    public const string TalentName = "Fervent Supremacy";
    public const double CooldownReductionSeconds = 10.0;
    public const int Charges = 4;
    public const double BuffSeconds = 15.0;
    public const double MultishotDamage = 1.5;

    /// <summary>Skystrider's Supremacy's buff as Fervent Supremacy makes it: <see cref="Charges"/> charges for <see cref="BuffSeconds"/>.</summary>
    public static readonly AuraDef Buff = SkystridersSupremacy.Buff.WithDuration(BuffSeconds);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<SkystridersSupremacy>() is not { } supremacy) return;
        supremacy.ShortenCooldown(CooldownReductionSeconds);
        supremacy.CastBuff = Buff;
        supremacy.ChargesGranted = Charges;
        hero.GetAbility<Multishot>()?.DamageMultipliers.Add((now, _) => hero.Auras.HasAura(Buff, now) ? MultishotDamage : 1.0);
    }
}
