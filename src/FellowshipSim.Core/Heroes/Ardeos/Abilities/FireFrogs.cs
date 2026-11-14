namespace FellowshipSim.Core.Heroes.Ardeos.Abilities;

/// <summary>
/// Ability 1268: instant. Conjures Fire Frogs to leap at the enemies, and each Fire Frog performs attacks before
/// dissipating. Each attack (<see cref="FireFrogAttack"/>) picks its enemy. Pyrophibian Frenzy spawns frogs of its
/// own (<see cref="Spawn"/>); the Fire Toad legendary turns frogs into toads (<see cref="ToadChance"/>).
/// </summary>
public partial class FireFrogs : ArdeosAbility
{
    public const int BaseAttacksPerFrog = 3;
    /// <summary>
    /// Combat logs (yMAZVJL9hTpFjqbC fight 7, final boss 32.1 @ 570-715s, the only enemy): a lone frog attacks at a fixed
    /// interval of server frames (<see cref="AttackIntervalSeconds"/>); a cast's frogs make their first attacks and their
    /// last at staggered delays after the cast.
    /// </summary>
    public const double FirstAttackSeconds = 1.7;
    public const double FrogStaggerSeconds = 0.15;
    public const double AttackIntervalSeconds = 20.0 / 30.0;

    /// <summary>The proc of each frog a cast conjures (always procs).</summary>
    public const string ConjuredProcKey = "fire_frogs_conjured";

    /// <summary>The proc of a frog that comes as a Fire Toad instead (<see cref="ToadChance"/>).</summary>
    public const string ToadProcKey = "fire_toad";

    public FireFrogAttack Attack { get; }

    /// <summary>Frogs a cast conjures (Frog Squad adds to it).</summary>
    public int Frogs { get; set; } = Constants.FireFrogAbilityNumFrogs;

    /// <summary>Attacks each frog performs (Frog Squad adds to it).</summary>
    public int AttacksPerFrog { get; set; } = BaseAttacksPerFrog;

    /// <summary>Chance each frog conjured comes as a Fire Toad instead (the Fire Toad legendary sets it).</summary>
    public double ToadChance { get; set; }

    /// <summary>Fire Toads a cast conjures besides its frogs (the Fire Toad legendary sets it).</summary>
    public int ToadsPerCast { get; set; }

    /// <summary>A Fire Toad exploding onto an enemy, in place of a frog's attacks (the Fire Toad legendary sets it).</summary>
    public Action<SimulationContext, EnemyTarget>? ToadExplosion { get; set; }

    private SimulationContext? _fight;

    public FireFrogs(Ardeos owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        TargetedOffensive = true;
        SetCooldown(AbilityCooldown);
        Attack = new FireFrogAttack(owner);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        // Each frog of the cast is a certain proc: it always comes, and a replay takes the log's attacks of the cast's frogs
        // as theirs, so they are no proof of Pyrophibian Frenzy's frogs.
        for (int frog = 0; frog < Frogs; frog++) ctx.Outcomes.RollProc(ConjuredProcKey, 1.0, ctx);
        Spawn(ctx, Frogs, FirstAttackSeconds);
        for (int toad = 0; toad < ToadsPerCast; toad++) ScheduleToad(ctx, FirstAttackSeconds + (Frogs + toad) * FrogStaggerSeconds);
    }

    /// <summary>
    /// Conjures <paramref name="frogs"/> Fire Frogs, each performing <see cref="AttacksPerFrog"/> attacks, the first
    /// <paramref name="firstAttackSeconds"/> from now.
    /// </summary>
    public void Spawn(SimulationContext ctx, int frogs, double firstAttackSeconds)
    {
        _fight = ctx;
        for (int frog = 0; frog < frogs; frog++)
        {
            // A Fire Toad explodes once, when the frog would have made its first attack.
            if (ToadChance > 0 && ctx.Outcomes.RollProc(ToadProcKey, ToadChance, ctx))
            {
                ScheduleToad(ctx, firstAttackSeconds + frog * FrogStaggerSeconds);
                continue;
            }
            for (int attack = 0; attack < AttacksPerFrog; attack++)
            {
                double delay = firstAttackSeconds + frog * FrogStaggerSeconds + attack * AttackIntervalSeconds;
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

    private void ScheduleToad(SimulationContext ctx, double delaySeconds)
    {
        if (ToadExplosion is not { } explode) return;
        _fight = ctx;
        ctx.Schedule(SimTime.FromSeconds(delaySeconds), c =>
        {
            if (_fight != c) return;
            var enemy = c.Outcomes.ChooseTarget(Attack.DamageLogId, c.ActiveEnemies, c);
            if (enemy != null) explode(c, enemy);
        }, AbilityName);
    }

    /// <summary>A new iteration: frogs of the last one stop.</summary>
    public void ResetState() => _fight = null;
}
