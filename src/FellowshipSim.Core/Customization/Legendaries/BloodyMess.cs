using FellowshipSim.Core.Heroes.Gunde;
using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 697 (Gunde's Carver's Sinister Apron, item 5218; Northlander's Helm, item 5899): "Your Grim Carve
/// spins 2 additional times." Each extra spin comes as long after the one before as the others. UNVERIFIED: the timing of the extra spins.
/// </summary>
public class BloodyMess : Legendary
{
    public const int PowerId = 697;
    public const int CloakItemId = 5218;
    public const int HelmItemId = 5899;
    public const int ExtraSpins = 2;

    public override int LegendaryPowerId => PowerId;
    public override string Name => "Bloody Mess";
    public override string HeroName => "Gunde";
    public override IReadOnlyList<int> ItemIds => [CloakItemId, HelmItemId];

    public override void Bind(Hero hero)
    {
        if (hero is Gunde gunde && gunde.GetAbility<GrimCarve>() is { } grimCarve) grimCarve.SpinCount += ExtraSpins;
    }
}
