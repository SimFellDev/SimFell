using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Core.Heroes.Mara.Talents;

/// <summary>
/// Talent 166: "Your Arachnid Assault deals more damage against enemies that have your Hemorrhaging Strike bleed
/// active." UNVERIFIED: no combat log has this talent.
/// </summary>
public partial class ArachnidOnslaught : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var mara = (Mara)hero;
        mara.GetAbility<ArachnidAssault>()?.TargetMultipliers.Add((enemy, ctx) => mara.Hemorrhage.IsOn(enemy, ctx) ? IncreasedDamageWhenBleedDamageMultiplier : 1.0);
    }
}
