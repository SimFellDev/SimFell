namespace FellowshipSim.Core.Heroes.Mara.Talents;

/// <summary>
/// Talent 112: "Each time you deal Poison Damage to an enemy you have a chance to generate Energy" (game data).
/// </summary>
public partial class VenomousDelight : Talent
{
    public const string ProcKey = "venomous_delight";

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var mara = (Mara)hero;
        hero.OnDamageDealt += (in hit) =>
        {
            if (hit.Amount <= 0 || hit.SimContext is not { } ctx || !mara.PoisonDamageIds.Contains(hit.AbilityId)) return;
            if (ctx.Outcomes.RollProc(ProcKey, Chance, ctx, hit.Target)) mara.GainEnergy(EnergyAdded, ctx);
        };
    }
}
