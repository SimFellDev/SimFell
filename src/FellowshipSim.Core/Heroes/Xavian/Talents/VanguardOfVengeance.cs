namespace FellowshipSim.Core.Heroes.Xavian.Talents;

/// <summary>
/// Talent 580: "Each time you Parry an attack you automatically counter-attack that enemy for 116% Strength physical
/// damage and gain 0.5% Mana. Each successful Parry also grants you the Vanguard of Vengeance buff for 20 seconds, stacking
/// up to 20 times - increasing your Spirit by +0.5% per stack. Your Parry chance is increased by +10%." Effect 2652 (20s,
/// 20 stacks, Spirit +0.005 each); the counter-attack is ability 1909 (93% of Strength, spread 0.2; Mana 0.5% of the
/// maximum, Sune.Talent.ParryCounterAttack.ManaGain), logged as effect 2651. A Parry triggers both, so they are defensive
/// procs: a replay fires the counter-attack at each logged Counter-Attack and a stack at each stack the log gains, a sim
/// never.
/// </summary>
public class VanguardOfVengeance : Talent
{
    public const int TalentId = 580;
    public const string TalentName = "Vanguard of Vengeance";
    public const int BuffEffectId = 2652;
    public const double BuffSeconds = 20.0;
    public const int MaxStacks = 20;
    public const double SpiritPerStack = 0.005;
    public const int CounterAttackEffectId = 2651;
    public const double CounterAttackCoefficient = 0.93;
    public const double CounterAttackManaShare = 0.005;
    public const string CounterAttackProcKey = "counter_attack";
    public const string StackProcKey = "vanguard_of_vengeance_parry";

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, BuffEffectId, BuffSeconds, MaxStacks,
        StatModifier.Add(StatType.Spirit, SpiritPerStack));

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var xavian = (Xavian)hero;
        var counter = new CounterAttack(xavian);
        hero.AddDefensiveProc(StackProcKey, (ctx, _) => hero.Auras.Apply(Buff, ctx.CurrentTime));
        hero.AddDefensiveProc(CounterAttackProcKey, (ctx, enemy) =>
        {
            if (enemy is not { IsSleeping: false }) return;
            counter.DealDamage(ctx, enemy);
            xavian.GainMana(Xavian.MaxMana * CounterAttackManaShare, ctx, enemy);
        });
    }

    /// <summary>The counter-attack's damage.</summary>
    private sealed class CounterAttack : Ability
    {
        public CounterAttack(Xavian owner) : base(owner, LogIds.Effect(CounterAttackEffectId), "Counter-Attack")
        {
            BaseCoefficient = CounterAttackCoefficient;
        }

        public override void Execute(SimulationContext ctx, EnemyTarget target) { }
    }
}
