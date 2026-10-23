using FellowshipSim.Core.Heroes.Mara;
using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 585 (Mara's Assassin's Shadow-Lined Cape, item 5218; Hungering Blade Helm, item 5899): "Your
/// Hemorrhaging Strike's bleed effect has 15% faster tick rate. Each time you deal damage to an enemy with Hemorrhaging
/// Strike's bleed effect, you have a 15% chance to summon a Shadowy Clone to assault that enemy with a 6 combo point
/// Queen's Fang at 100% Power." UNVERIFIED: no combat log has it; the clone's Queen's Fang is taken to be logged as
/// Queen's Fang (ability 1201), at once.
/// </summary>
public class FromTheShadows : Legendary
{
    public const int PowerId = 585;
    public const int CapeItemId = 5218;
    public const int HelmItemId = 5899;
    public const double TickRateBonus = 0.15;
    public const double Chance = 0.15;
    public const int CloneComboPoints = 6;
    public const double ClonePower = 1.0;
    public const string ProcKey = "from_the_shadows";

    public override int LegendaryPowerId => PowerId;
    public override string Name => "From the Shadows";
    public override string HeroName => "Mara";
    public override IReadOnlyList<int> ItemIds => [CapeItemId, HelmItemId];

    public override void Bind(Hero hero)
    {
        if (hero is not Mara mara || mara.GetAbility<QueensFang>() is not { } queensFang) return;
        mara.Hemorrhage.TickRateBonuses.Add(_ => TickRateBonus);
        mara.Hemorrhage.Ticked += (ctx, target) =>
        {
            if (ctx.Outcomes.RollProc(ProcKey, Chance, ctx, target)) queensFang.CloneStrike(ctx, target, CloneComboPoints, ClonePower);
        };
    }
}
