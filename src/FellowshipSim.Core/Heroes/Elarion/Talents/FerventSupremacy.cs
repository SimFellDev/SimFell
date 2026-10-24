using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>
/// Talent 136: "Your Skystrider's Supremacy's cooldown is reduced by 10 seconds and grants 4 charges of Empowered Multishot
/// that lasts for up to 15 seconds. While Fervant Supremacy is active, your Multishot deals 50% more damage." Effect 2009
/// talent ChargesCooldownAndDuration (duration 15), effect 2010 DamageBonus 1.5.
/// Combat logs (fXyzwGcQkTrLD34R fight 4, pWmcV42K39GHTLQX fight 23): the cast applies effect 2009 at 4 stacks, and each
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

    /// <summary>Skystrider's Supremacy's buff as Fervent Supremacy makes it: 4 charges for 15 seconds.</summary>
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
