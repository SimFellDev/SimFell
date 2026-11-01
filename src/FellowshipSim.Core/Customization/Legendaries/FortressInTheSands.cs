namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 579 (Xavian's Light of Azamir Greaves, item 5900): "Your Solar Shield now also reduces all damage taken
/// by 20%. Fortress in the Sands: When you take damage that would kill you, Solar Shield is applied to you for free. Fortress
/// in the Sands can occur once every 30 seconds." No damage.
/// </summary>
public class FortressInTheSands : Legendary
{
    public const int PowerId = 579;
    public const int GreavesItemId = 5900;

    public override int LegendaryPowerId => PowerId;
    public override string Name => "Fortress in the Sands";
    public override string HeroName => "Xavian";
    public override IReadOnlyList<int> ItemIds => [GreavesItemId];

    public override void Bind(Hero hero) { }
}
