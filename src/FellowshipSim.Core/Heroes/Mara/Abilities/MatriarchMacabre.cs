namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Ability 1420 (Spirit ability) / effect 2039: "For the next 20 seconds, you deal 20% more damage and your Queen's Fang
/// and Arachnid Assault abilities summon two shadowy clones that both imitate the attack for 50% of your power"; grants
/// Spirit of Heroism (+30% Haste).
/// Combat logs (Fc8v6yWxD14GQMnV fight 19 @ 28.487, 32.006): the clones' hits are logged under the attack's own ID and
/// keep its bonuses (Deadly Scheme's crit too). The first clone strikes 0.335s after the cast, the second 0.335s after
/// the first is done; a clone's Arachnid Assault hits one enemy per server frame.
/// </summary>
public class MatriarchMacabre : MaraAbility
{
    public const int AbilityId = 1420;
    public const string AbilityName = "Matriarch Macabre";
    public const int BuffEffectId = 2039;
    public const double BuffSeconds = 20.0;
    public const double BuffDamage = 1.2;
    public const int Clones = 2;
    public const double ClonePower = 0.5;
    public const double CloneDelaySeconds = 0.335;

    public static readonly AuraDef Buff = AuraDef.Effect("Matriarch Macabre", BuffEffectId, BuffSeconds,
        modifiers: StatModifier.More(StatType.DamageScale, BuffDamage));

    /// <summary>
    /// The name of a shadowy clone's hits of <paramref name="abilityName"/>: the log ID is the ability's, the name tells
    /// the clone's damage apart.
    /// </summary>
    public static string CloneHitName(string abilityName) => abilityName + " (shadowy clone)";

    public MatriarchMacabre(Mara owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Spirit)
    {
        IgnoresGcd = true;
    }

    /// <summary>Mara's Spirit ability: it needs and spends the Spirit ability's cost in Spirit Points.</summary>
    protected override bool CanCast(SimulationContext ctx, bool explain, out string? reason)
    {
        double points = Owner.ResourceAmount(Hero.SpiritPoints);
        if (points < Owner.SpiritAbilityCost)
        {
            reason = explain ? $"{Name} needs {Owner.SpiritAbilityCost:0} Spirit Points (have {points:0.#})" : null;
            return false;
        }
        return base.CanCast(ctx, explain, out reason);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        var hero = Mara;
        hero.Resources.Consume(Hero.SpiritPoints, Math.Min(hero.SpiritAbilityCost, hero.ResourceAmount(Hero.SpiritPoints)));
        hero.ActivateSpiritOfHeroism(ctx);
        hero.Auras.Apply(Buff, ctx.CurrentTime);
    }

    /// <summary>
    /// While Matriarch Macabre is active, two clones repeat an attack of <paramref name="hits"/> hits:
    /// <paramref name="strike"/> deals it with the power given (50%).
    /// </summary>
    public void Imitate(SimulationContext ctx, int hits, Action<SimulationContext, double> strike)
    {
        if (!Mara.Auras.HasAura(Buff, ctx.CurrentTime)) return;
        double sweep = Math.Max(0, hits - 1) * PeriodicEffect.ServerFrameSeconds;
        for (int clone = 0; clone < Clones; clone++)
        {
            double delay = CloneDelaySeconds * (clone + 1) + sweep * clone;
            ctx.Schedule(SimTime.FromSeconds(delay), c => strike(c, ClonePower), "Shadowy clone");
        }
    }
}
