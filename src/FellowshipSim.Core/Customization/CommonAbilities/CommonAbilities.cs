namespace FellowshipSim.Core.Customization.CommonAbilities;

/// <summary>The shared abilities: the relic abilities, the mount and the world's.</summary>
public static class CommonAbilities
{
    public static IEnumerable<Ability> For(Hero hero) =>
    [
        new Revive(hero),
        new MajorDispel(hero),
        new Chickenize(hero),
        new MajorInvisibility(hero),
        new RelicTeleport(hero),
        new BloodriteFervor(hero),
        new ConjurePortal(hero),
        new Anchor(hero),
        new MakingCamp(hero),
        new MountSummon(hero),
    ];
}
