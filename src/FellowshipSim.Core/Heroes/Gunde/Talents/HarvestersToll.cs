using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Core.Heroes.Gunde.Talents;

/// <summary>
/// Talent 767 / effect 3218: "Your Rupture ability grants you Harvester's Toll, increasing all damage you deal for a
/// time." The effect's own text gives a different value from the talent's; the talent's is used. Combat logs
/// (Y4wpb2f3gc8LqJDX fight 25 @ 26.616): it comes with the Rupture before its hit.
/// </summary>
public partial class HarvestersToll : Talent
{
    public const int EffectId = 3218;

    public static readonly AuraDef Buff = AuraDef.Effect("Harvesters Toll", EffectId, Constants.IncreasedDamageOnSelfDuration,
        modifiers: StatModifier.More(StatType.DamageScale, 1.0 + Constants.IncreasedDamageOnSelfDamageScale));

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        hero.OnCastStarted += (ability, _, now) =>
        {
            if (ability is Rupture) hero.Auras.Apply(Buff, now);
        };
    }
}
