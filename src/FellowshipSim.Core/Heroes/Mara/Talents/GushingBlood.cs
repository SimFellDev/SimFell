using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Core.Heroes.Mara.Talents;

/// <summary>
/// Talent 122: "While Maiden of Death is active, your Hemorrhaging Strike applies it's bleed to up to 4 additional nearby
/// enemies. Your Hemorrhaging Strike's Bleed deals 50% more damage to enemies that are afflicted by your Seething Poison."
/// UNVERIFIED: no combat log has this talent.
/// </summary>
public class GushingBlood : Talent
{
    public const int TalentId = 122;
    public const string TalentName = "Gushing Blood";
    public const int ExtraEnemies = 4;
    public const double SeethingDamage = 1.5;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var mara = (Mara)hero;
        mara.GetAbility<HemorrhagingStrike>()?.ExtraBleedTargets.Add((ctx, target) =>
            hero.Auras.HasAura(MaidenOfDeath.Buff, ctx.CurrentTime) ? ctx.ActiveEnemiesExcept(target).Take(ExtraEnemies) : []);
        mara.Hemorrhage.TargetMultipliers.Add((enemy, ctx) => mara.SeethingPoison.IsOn(enemy, ctx) ? SeethingDamage : 1.0);
    }
}
