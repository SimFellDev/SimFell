using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>
/// Talent 158: Fire Frogs conjures additional frogs, every Fire Frog leaps on enemies one additional time, and Fire Frogs
/// deal more damage. The damage increase applies to the damage over time as well as to the hits it is a share of. In the
/// three logged fights with the talent (yMAZVJL9hTpFjqbC fight 7, Fc8v6yWxD14GQMnV fight 19, YC861NTRgLbmw4Qa fight 31),
/// the replay's effect matches the logged damage that way, and falls short with the increase on the hits alone.
/// </summary>
public partial class FrogSquad : Talent
{
    public const int ExtraAttacks = 1;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var frogs = hero.GetAbility<FireFrogs>();
        if (frogs == null) return;
        frogs.Frogs += Constants.FireFrogAbilityTalentExtraFrogsNumExtraFrogs;
        frogs.AttacksPerFrog += ExtraAttacks;
        frogs.Attack.AddDamageMultiplier(Constants.DirectDamageIncreaseDamageScale);
        frogs.Attack.DotFraction *= Constants.DirectDamageIncreaseDamageScale;
    }
}
