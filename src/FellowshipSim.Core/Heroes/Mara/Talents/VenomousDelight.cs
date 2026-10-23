namespace FellowshipSim.Core.Heroes.Mara.Talents;

/// <summary>
/// Talent 112: "Each time you deal Poison Damage to an enemy you have a 10% chance to generate 10 Energy" (game data
/// Mara.Talent.PoisonDamageProcEnergyGain: 10 Energy, chance 0.1).
/// </summary>
public class VenomousDelight : Talent
{
    public const int TalentId = 112;
    public const string TalentName = "Venomous Delight";
    public const double Chance = 0.1;
    public const double EnergyGained = 10;
    public const string ProcKey = "venomous_delight";

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var mara = (Mara)hero;
        hero.OnDamageDealt += (in hit) =>
        {
            if (hit.Amount <= 0 || hit.SimContext is not { } ctx || !mara.PoisonDamageIds.Contains(hit.AbilityId)) return;
            if (ctx.Outcomes.RollProc(ProcKey, Chance, ctx, hit.Target)) mara.GainEnergy(EnergyGained, ctx);
        };
    }
}
