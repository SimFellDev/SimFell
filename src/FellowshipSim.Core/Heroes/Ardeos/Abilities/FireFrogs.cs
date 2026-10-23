namespace FellowshipSim.Core.Heroes.Ardeos.Abilities;

/// <summary>
/// Ability 1268: instant, 45s. "Conjure five Fire Frogs to leap at your enemies ... Each Fire Frog will perform 3 attacks
/// before dissipating." Each attack (<see cref="FireFrogAttack"/>) picks its enemy. Pyrophibian Frenzy spawns frogs of its
/// own (<see cref="Spawn"/>).
/// </summary>
public class FireFrogs : ArdeosAbility
{
    public const int AbilityId = 1268;
    public const string AbilityName = "Fire Frogs";
    public const double CooldownSeconds = 45.0;
    public const int BaseFrogs = 5;
    public const int BaseAttacksPerFrog = 3;
    /// <summary>
    /// Combat logs (yMAZVJL9hTpFjqbC fight 7): a cast's first attack lands 1.7-1.9s after it; a lone frog attacks every
    /// 40 server frames (Pyrophibian Frenzy frogs @ 248.99-253.90s: hits every 0.668s alternating between two enemies).
    /// </summary>
    public const double FirstAttackSeconds = 1.7;
    public const double FrogStaggerSeconds = 0.1;
    public const double AttackIntervalSeconds = 40.0 / 30.0;

    public FireFrogAttack Attack { get; }

    /// <summary>Frogs a cast conjures (Frog Squad adds to it).</summary>
    public int Frogs { get; set; } = BaseFrogs;

    /// <summary>Attacks each frog performs (Frog Squad adds to it).</summary>
    public int AttacksPerFrog { get; set; } = BaseAttacksPerFrog;

    private SimulationContext? _fight;

    public FireFrogs(Ardeos owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Core)
    {
        SetCooldown(CooldownSeconds);
        Attack = new FireFrogAttack(owner);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        Spawn(ctx, Frogs);
    }

    /// <summary>Conjures <paramref name="frogs"/> Fire Frogs, each performing <see cref="AttacksPerFrog"/> attacks.</summary>
    public void Spawn(SimulationContext ctx, int frogs)
    {
        _fight = ctx;
        for (int frog = 0; frog < frogs; frog++)
        {
            for (int attack = 0; attack < AttacksPerFrog; attack++)
            {
                double delay = FirstAttackSeconds + frog * FrogStaggerSeconds + attack * AttackIntervalSeconds;
                ctx.Schedule(SimTime.FromSeconds(delay), Leap, AbilityName);
            }
        }
    }

    private void Leap(SimulationContext ctx)
    {
        if (_fight != ctx) return;
        var enemy = ctx.Outcomes.ChooseTarget(Attack.DamageLogId, ctx.ActiveEnemies, ctx);
        if (enemy != null) Attack.DealDamage(ctx, enemy);
    }

    /// <summary>A new iteration: frogs of the last one stop.</summary>
    public void ResetState() => _fight = null;
}
