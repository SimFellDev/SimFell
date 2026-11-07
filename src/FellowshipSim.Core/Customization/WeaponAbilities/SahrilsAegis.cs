namespace FellowshipSim.Core.Customization.WeaponAbilities;

/// <summary>
/// Weapon ability 159 (weapon power 352): "You are protected by the divine flame of Sahril, granting you a shield for up
/// to 18 seconds ... When the shield expires it explodes on to enemies within a 1000 radius, dealing 2241% fire damage to
/// them. Can be cast during Global Cooldown." Cooldown 90s; the explosion falls off as sqrt(1/N) beyond 1 enemy (game
/// data targetCountFalloff). The shield is effect 77 (18 seconds, "Explodes upon removal"); it lasts its 18 seconds and
/// explodes as it expires. UNVERIFIED: the explosion's damage ID is taken to be 159, as Sahril's Wrath's hits are logged
/// as 157; the downloaded combat logs have this ability in none of their fights.
/// </summary>
public class SahrilsAegis : Ability
{
    public const int AbilityId = 159;
    public const string AbilityName = "Sahril's Aegis";
    public const double Coefficient = 22.41;
    public const double CooldownSeconds = 90.0;
    public const double TargetThreshold = 1;
    public const int ShieldEffectId = 77;
    public const double ShieldSeconds = 18.0;

    /// <summary>The shield on the hero until it explodes.</summary>
    public static readonly AuraDef Shield = AuraDef.Effect(AbilityName, ShieldEffectId, ShieldSeconds);

    public SahrilsAegis(Hero owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Weapon)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(CooldownSeconds);
        AoeTargetThreshold = TargetThreshold;
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        var shield = Owner.Auras.Apply(Shield, ctx.CurrentTime);
        // The explosion comes on the shield's actual expiry, which includes the server-frame lag.
        ctx.ScheduleAt(shield.ExpiresAt, c =>
        {
            if (Owner.Auras.HasAura(Shield, c.CurrentTime)) return;
            IReadOnlyList<EnemyTarget> enemies = c.ActiveEnemies.Count == 0 && target is { IsSleeping: false } ? [target] : c.ActiveEnemies;
            DealAoeDamage(c, enemies);
        }, AbilityName);
    }
}
