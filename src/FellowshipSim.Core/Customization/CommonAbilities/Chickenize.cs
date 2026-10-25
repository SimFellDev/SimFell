namespace FellowshipSim.Core.Customization.CommonAbilities;

/// <summary>
/// Ability 1478 (a relic ability): "Instantly turn your target into a chicken. Chickenize! can only be used a
/// limited amount of times per dungeon. The charges are shared by everyone in the party. ... Can be used during Global
/// Cooldown." Cooldown 180s (game data; 4 uses per dungeon, the party's). No damage.
/// </summary>
public class Chickenize : Ability
{
    public const int AbilityId = 1478;
    public const string AbilityName = "Chickenize!";
    public const double CooldownSeconds = 180.0;

    public Chickenize(Hero owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Relic)
    {
        SetCooldown(CooldownSeconds);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) => TriggerCooldown(ctx);
}
