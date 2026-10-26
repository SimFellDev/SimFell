namespace FellowshipSim.Tests.Functional.GearShared;

/// <summary>
/// Two damage-multiplier effects of the game that the engine does not have. They are here so they are not forgotten; both are skipped.
/// </summary>
public class SharedUnfoundEffects
{
    /// <summary>
    /// Player: Overlord Varux in Scryer's Peak gives a buff that multiplies your damage.
    /// Dev: nothing to test yet.
    /// TODO: we could not find this buff in the engine or the game data (no effect or aura of that name, nothing in Hero.cs or Engine/). Find its
    /// ID, value and duration, then test that it multiplies the damage of every hero's hits by that value, over all six heroes.
    /// </summary>
    [Fact(Skip = "The buff from Overlord Varux in Scryer's Peak (a damage multiplier) is not in the engine and we could not find it: see the TODO")]
    public void The_Varux_Buff_Multiplies_Damage() { }

    /// <summary>
    /// Player: some enemies apply a debuff to themselves that makes them take more damage; every hero's damage against them is multiplied.
    /// Dev: nothing to test yet.
    /// TODO: a separate effect from the Varux buff. Not in the engine (no enemy self-applied damage-taken debuff). Find which enemies have it, the
    /// bonus and its duration, then test that damage against such an enemy is multiplied and damage against others is not.
    /// </summary>
    [Fact(Skip = "Enemies' self-applied 'takes more damage' debuff is not in the engine: see the TODO")]
    public void An_Enemy_With_Its_Own_Damage_Taken_Debuff_Takes_More_Damage() { }
}
