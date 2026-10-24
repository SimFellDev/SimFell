namespace FellowshipSim.Core.Heroes.Elarion.Abilities;

/// <summary>
/// Lunarlight Salvo (ability 1309, damage effect 1877): "Ability damage you deal to enemies with Lunarlight Mark has a 25%
/// chance to trigger Lunarlight Salvo, causing a beam of celestial energy to strike them from above, dealing magic damage.
/// Critical Strikes have a 50% chance to trigger Lunarlight Salvo." Each salvo takes a stack of Lunarlight Mark off the
/// enemy. Heartseeker Barrage's tooltip: "Damage dealt by Heartseeker Barrage that triggers a Lunarlight Salvo has a 25%
/// chance to cause it to Erupt" (effect 2832), striking the enemies near the target.
/// Combat logs (fXyzwGcQkTrLD34R fight 4, pWmcV42K39GHTLQX fight 23, 4,984 salvos): a salvo comes with the hit that
/// triggered it, on the same enemy and in the same server frame, from 23-34% of the hits that do not crit and 46-57% of
/// the crits on a marked enemy; from Starfall Volley, Multishot, Highwind Arrow, Heartseeker Barrage, Celestial Shot,
/// Focused Shot, Chronoshift and the augmentations' hits (Amethyst Splinters, Emerald Judgement, Ruby Storm), but not
/// from Shoot (5 of 603 hits on a marked enemy, the same frame as another hit).
/// </summary>
public sealed class LunarlightSalvo : Ability
{
    public const int SalvoAbilityId = 1309;
    public const string AbilityName = "Lunarlight Salvo";
    public const int DamageEffectId = 1877;
    /// <summary>Lunarlight Mark placeholder LunarSalvoDmg: 219% of Agility.</summary>
    public const double Coefficient = 2.19;
    public const double Chance = 0.25;
    public const double CritChance = 0.50;
    public const string ProcKey = "lunarlight_salvo";
    /// <summary>A salvo that leaves the stack of Lunarlight Mark (Lunarlight Affinity: Starfall Volley's).</summary>
    public const string KeptMarkProcKey = "lunarlight_salvo_kept_mark";

    public const int EruptEffectId = 2832;
    public const string EruptName = "Lunarlight Salvo: Erupt";
    /// <summary>Heartseeker Barrage placeholder AoeLunarLightDmg: 219% of Agility.</summary>
    public const double EruptCoefficient = 2.19;
    public const double EruptChance = 0.25;
    /// <summary>Ability 1309 targetCountFalloff: Lunarlight Salvo: Erupt, threshold 12.</summary>
    public const double EruptTargetThreshold = 12;
    public const string EruptProcKey = "lunarlight_salvo_erupt";

    /// <summary>Multipliers on the chance of a hit with a damage ID to call down a salvo (Lunar Fury).</summary>
    public List<Func<int, double>> ChanceMultipliers { get; } = new();

    /// <summary>Damage IDs whose salvos leave the stack of Lunarlight Mark (Lunarlight Affinity: Starfall Volley).</summary>
    public HashSet<int> KeepsMarkFor { get; } = new();

    /// <summary>Damage IDs whose hits never call down a salvo: Shoot, the salvo and its eruption.</summary>
    private readonly HashSet<int> _noSalvo;

    private readonly Erupt _erupt;

    public LunarlightSalvo(Elarion owner) : base(owner, SalvoAbilityId, AbilityName)
    {
        BaseCoefficient = Coefficient;
        DamageLogId = LogIds.Effect(DamageEffectId);
        _erupt = new Erupt(owner);
        _noSalvo = [Shoot.AbilityId, DamageLogId, _erupt.DamageLogId];
        owner.OnDamageDealt += OnHit;
    }

    /// <summary>The eruption's damage, for talents that add to it.</summary>
    public Ability Eruption => _erupt;

    public override void Execute(SimulationContext ctx, EnemyTarget target) { }

    private void OnHit(in DamageContext hit)
    {
        if (hit.Target is not { } enemy || hit.SimContext is not { } ctx || hit.Amount <= 0 || _noSalvo.Contains(hit.AbilityId)) return;
        var now = hit.Timestamp;
        if (!enemy.Auras.HasAura(LunarlightMark.Debuff, now)) return;
        double chance = hit.IsCrit ? CritChance : Chance;
        foreach (var m in ChanceMultipliers) chance *= m(hit.AbilityId);
        bool keepsMark = KeepsMarkFor.Contains(hit.AbilityId);
        if (!ctx.Outcomes.RollProc(keepsMark ? KeptMarkProcKey : ProcKey, Math.Min(1.0, chance), ctx, enemy)) return;
        if (!keepsMark) enemy.Auras.ConsumeStack(LunarlightMark.Debuff, now);
        DealDamage(ctx, enemy);
        if (hit.AbilityId == HeartseekerBarrage.AbilityId && ctx.Outcomes.RollProc(EruptProcKey, EruptChance, ctx))
        {
            _erupt.Strike(ctx, enemy);
        }
    }

    /// <summary>The eruption of a salvo Heartseeker Barrage called down: magic damage to the enemies near the target.</summary>
    private sealed class Erupt : Ability
    {
        public Erupt(Elarion owner) : base(owner, SalvoAbilityId, EruptName)
        {
            BaseCoefficient = EruptCoefficient;
            DamageLogId = LogIds.Effect(EruptEffectId);
            AoeTargetThreshold = EruptTargetThreshold;
        }

        public override void Execute(SimulationContext ctx, EnemyTarget target) { }

        /// <summary>The enemies near <paramref name="target"/>, not the target itself; a replay takes them from the log.</summary>
        public void Strike(SimulationContext ctx, EnemyTarget target) => DealAoeDamage(ctx, ctx.ActiveEnemiesExcept(target));
    }
}
