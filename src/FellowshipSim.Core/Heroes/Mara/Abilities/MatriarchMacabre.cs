namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Ability 1420 (Spirit ability) / effect 2039: "For the next 20 seconds, you deal 20% more damage and your Queen's Fang
/// and Arachnid Assault abilities summon two shadowy clones that both imitate the attack for 50% of your power"; grants
/// Spirit of Heroism (+30% Haste).
/// Combat logs (Fc8v6yWxD14GQMnV fight 19 @ 28.487, 32.006): the clones' hits are logged under the attack's own ID and
/// keep its bonuses (Deadly Scheme's crit too). The first clone strikes 0.335s after the cast, the second 0.335s after
/// the first is done; a clone's Arachnid Assault hits one enemy per server frame. Both clones of a Queen's Fang keep Mara's
/// numbers at the cast: over the 2,580 clone hits in 23 fights, 77 after Matriarch Macabre ended are 1.36x the engine's read
/// at the strike, and 39 after The Philosopher came 0.79x (T2qLPpmMkrAtJ9B1 fight 18 @ 1249.421, pWmcV42K39GHTLQX fight 23
/// @ 276.428).
/// Game data (build 26320967): a clone applies a copy of the cast's spec, `GA_Mara_Passive_CopySpawner` offset 4412
/// `CloneSpecHandle` (0x144A62970 keeps captures and SetByCaller values) and offset 2688. `SetEffectContext` at offset 4600
/// re-snapshots in the cast's frame (0x144A6F7D0). `CRAgilityDamageCalculation` (0x1450D73F0): source attributes are
/// snapshots, target attributes are read at the strike, and each application rolls its own crit (`Execute_Implementation`
/// 0x1450DCF40, roll 0x14510C4D0).
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
    /// Mara's numbers at a cast with no enemy's multipliers, and whether Maiden of Death was active: a target multiplier
    /// of the engine's, and a buff of Mara's.
    /// </summary>
    public readonly record struct CloneCast(MaraAbility Attack, SimulationContext.HitNumbers Numbers, bool MaidenOfDeathActive);

    /// <summary>The numbers both clones keep from a cast of <paramref name="attack"/>, at 50% of its power.</summary>
    public CloneCast KeepCast(MaraAbility attack, SimulationContext ctx, double damageMultiplier, double critChance) =>
        new(attack, attack.NumbersNow(ctx, null, damageMultiplier * ClonePower, critChance), Mara.Auras.HasAura(MaidenOfDeath.Buff, ctx.CurrentTime));

    /// <summary>A clone's hit on <paramref name="enemy"/>: Mara's numbers at the cast, the enemy's multipliers at the strike.</summary>
    public SimulationContext.HitNumbers StrikeNumbers(SimulationContext ctx, CloneCast cast, EnemyTarget enemy)
    {
        var now = ctx.CurrentTime;
        double against = Mara.DamageMultiplierAgainst(enemy, now);
        bool maidenNow = Mara.Auras.HasAura(MaidenOfDeath.Buff, now);
        if (maidenNow != cast.MaidenOfDeathActive)
        {
            double maiden = Mara.GetAbility<MaidenOfDeath>()!.DamageAgainst(enemy);
            against = maidenNow ? against / maiden : against * maiden;
        }
        foreach (var multiplier in cast.Attack.TargetMultipliers) against *= multiplier(enemy, ctx);
        var numbers = cast.Numbers;
        return numbers with
        {
            BaseDamage = numbers.BaseDamage * against,
            CritChance = numbers.CritChance + Mara.CritChanceAgainst(enemy, now) - Mara.CritChance(now),
        };
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
