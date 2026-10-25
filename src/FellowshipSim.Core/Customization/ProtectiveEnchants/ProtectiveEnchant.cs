using FellowshipSim.Core.Customization.Augmentations;

namespace FellowshipSim.Core.Customization.ProtectiveEnchants;

/// <summary>
/// A protective enchant: the necklace's augmentation (ItemTrait.TraitCategory.Necklace). Each one is defensive or
/// utility and has no effect on damage, so the engine leaves it out.
/// </summary>
public abstract class ProtectiveEnchant : Augmentation
{
    /// <summary>Protective enchants a character can have at once (per the user: "Defensives are pick three").</summary>
    public const int MaxPerBuild = 3;

    public override void Bind(Hero hero) { }
}
