namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Ability 1468: refills your Energy and Combo Points and resets all your Hero Ability cooldowns. Can
/// be used during Global Cooldown. Macabre Stratagem replaces it (<see cref="MacabreStratagem"/>).
/// Game data: the cooldown reduction for all abilities skips an ability
/// with AbilityType.Skill.Secondary, AbilityType.Weapon, CustomAssetTags.Mara.ResetCooldownRefillResources, AbilityType.SetBonus
/// or AbilityType.Trait in its AbilityTags or ParentTags, and leaves the global cooldown alone.
/// </summary>
public partial class FinalStratagem : MaraAbility
{
    public FinalStratagem(Mara owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        SetCooldown(AbilityCooldown);
        IgnoresGcd = true;
    }

    protected override bool CanCast(SimulationContext ctx, bool explain, out string? reason)
    {
        if (Mara.HasMacabreStratagem)
        {
            reason = explain ? $"{Name} is replaced by Macabre Stratagem" : null;
            return false;
        }
        return base.CanCast(ctx, explain, out reason);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        var hero = Mara;
        TriggerCooldown(ctx);
        hero.GainEnergy(Mara.HeroMaxResources, ctx);
        hero.GainComboPoints(Mara.HeroMaxResourcesSecond, ctx);
        foreach (var ability in hero.Abilities)
        {
            if (ability == this || ability.Category is AbilityCategory.Spirit or AbilityCategory.Weapon || ability is Attack) continue;
            if (hero.Cooldowns.TryGetValue(ability.Name, out var state)) state.ResetAll();
        }
    }
}
