using FellowshipSim.Core.Heroes.Rime;

namespace FellowshipSim.Core.APL.Expressions;

public class ConstantExpression : IAplExpression
{
    public double Value { get; }

    public ConstantExpression(double value) => Value = value;

    public bool Evaluate(Hero hero, SimulationContext ctx) => Math.Abs(Value) > 0.00001;

    public double EvaluateNumeric(Hero hero, SimulationContext ctx) => Value;

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// A value read from the hero or the fight, written as a path: a name in <see cref="Named"/>, a resource ("winter_orbs",
/// or "resource.winter_orbs") or the room left in one ("spirit.deficit"), "&lt;scope&gt;.&lt;name&gt;.&lt;property&gt;" with
/// a scope and property in <see cref="Scopes"/> ("dot" is "debuff"), "&lt;scope&gt;.&lt;name&gt;" with a scope in
/// <see cref="BuildScopes"/> (1 when the build has it), "active_dot.&lt;name&gt;" (the enemies in combat with that debuff),
/// or "variable.&lt;name&gt;".
/// </summary>
public class PropertyExpression : IAplExpression
{
    public const string ResourcePrefix = "resource.";
    public const string DeficitSuffix = ".deficit";
    public const string BuffScope = "buff";
    public const string DebuffScope = "debuff";
    /// <summary>SimulationCraft's name for a damage over time effect: the same as <see cref="DebuffScope"/>.</summary>
    public const string DotScope = "dot";
    public const string ActiveDotScope = "active_dot";
    public const string CooldownScope = "cooldown";
    public const string ActionScope = "action";
    public const string ChannelScope = "channel";
    public const string TalentScope = "talent";
    public const string BlessingScope = "blessing";
    public const string LegendaryScope = "legendary";
    public const string WeaponScope = "weapon";
    public const string WeaponTraitScope = "weapon_trait";
    public const string VariableScope = "variable";
    public const string PrevScope = "prev";
    public const string PrevGcdScope = "prev_gcd";

    /// <summary>The application of a debuff applied first: <c>debuff.&lt;name&gt;.oldest.&lt;property&gt;</c>.</summary>
    public const string OldestPick = "oldest";
    /// <summary>The application of a debuff applied last: <c>debuff.&lt;name&gt;.newest.&lt;property&gt;</c>.</summary>
    public const string NewestPick = "newest";

    /// <summary>The properties one application of a debuff has, after <see cref="OldestPick"/> or <see cref="NewestPick"/>.</summary>
    private static readonly string[] PickedProperties = ["remains", "refreshable"];

    /// <summary>A debuff's properties: each aura property, then each picked application's ("oldest.remains").</summary>
    private static IReadOnlyCollection<string> DebuffProperties =>
        [.. AuraProperties.Keys, .. new[] { OldestPick, NewestPick }.SelectMany(pick => PickedProperties.Select(p => $"{pick}.{p}"))];

    /// <summary>SimulationCraft's pandemic window: an aura is refreshable in the last part of its duration.</summary>
    public const double RefreshableFraction = 0.3;

    /// <summary>A countdown's value when its time never comes: no such pull follows, or the fight has no set end.</summary>
    public const double NeverIn = 10_000.0;

    /// <summary>
    /// Named values that count the seconds down to a time (<see cref="SecondsUntil"/>), by their path: they fall by a second
    /// per second between events.
    /// </summary>
    public static IReadOnlyDictionary<string, Func<SimulationContext, SimTime>> Countdowns { get; } =
        new Dictionary<string, Func<SimulationContext, SimTime>>
        {
            ["target.time_to_die"] = ctx => ctx.Player.CurrentTarget is not { } target ? SimTime.Zero
                : target.DiesAt != SimTime.Infinite ? target.DiesAt : ctx.FightEnd,
            ["dungeon_event.fight.remains"] = ctx => ctx.FightEnd,
            ["dungeon_event.adds.in"] = ctx => ctx.NextTrashAt,
            ["dungeon_event.boss.in"] = ctx => ctx.NextBossAt,
        };

    /// <summary>The seconds from now until <paramref name="end"/>; <see cref="NeverIn"/> when it never comes.</summary>
    public static double SecondsUntil(SimulationContext ctx, SimTime end) =>
        end == SimTime.Infinite ? NeverIn : (end - ctx.CurrentTime).TotalSeconds;

    /// <summary>The seconds from <paramref name="time"/> until the end of the current target's cast; 0 when it is not casting.</summary>
    public static double CastRemains(SimulationContext ctx, SimTime time) =>
        ctx.Player.CurrentTarget is { IsCasting: true } target ? Math.Max(0.0, (target.CastEnd - time).TotalSeconds) : 0.0;

    /// <summary>The enemies in combat that are bosses (or are not).</summary>
    private static int CountActive(SimulationContext ctx, bool boss)
    {
        int count = 0;
        foreach (var enemy in ctx.ActiveEnemies)
        {
            if (enemy.IsBoss == boss) count++;
        }
        return count;
    }

    /// <summary>Values named by their whole path.</summary>
    public static IReadOnlyDictionary<string, AplDescribed<Func<Hero, SimulationContext, double>>> Named { get; } =
        new Dictionary<string, AplDescribed<Func<Hero, SimulationContext, double>>>
        {
            ["active_enemies"] = new(new("Enemies in combat", "The number of enemies in combat. The minimum is 1."),
                (_, ctx) => Math.Max(1, ctx.ActiveEnemies.Count)),
            ["target.health.pct"] = new(new("Target health %", "The health of the target, in percent. 100 if there is no target."),
                (_, ctx) => ctx.Player.CurrentTarget?.HealthPercent ?? 100.0),
            ["target.health"] = new(new("Target health", "The current health of the target. 0 if there is no target."),
                (_, ctx) => ctx.Player.CurrentTarget?.CurrentHealth ?? 0.0),
            ["target.time_to_die"] = new(new("Target time to die", "Seconds until the target dies: from its health loss per second since it arrived, its group's dies or the end of its pull duration, or the end of a training dummy fight. The end of the fight before a dungeon enemy with hit points takes damage. 0 if there is no target."),
                (_, ctx) => SecondsUntil(ctx, Countdowns["target.time_to_die"](ctx))),
            ["target.is_boss"] = new(new("Target is a boss", "1 if the target is a boss. Otherwise 0."),
                (_, ctx) => ctx.Player.CurrentTarget is { IsBoss: true } ? 1.0 : 0.0),
            ["target.is_priority"] = new(new("Target is a priority target", "1 if the target is a priority target of a trash pull. Otherwise 0."),
                (_, ctx) => ctx.Player.CurrentTarget is { IsPriority: true } ? 1.0 : 0.0),
            ["target.casting"] = new(new("Target is casting", "1 if the target is casting an interruptible ability. Otherwise 0."),
                (_, ctx) => ctx.Player.CurrentTarget is { IsCasting: true } ? 1.0 : 0.0),
            ["target.cast.remains"] = new(new("Target cast time remaining", "Seconds until the cast of the target ends. 0 if the target is not casting."),
                (_, ctx) => CastRemains(ctx, ctx.CurrentTime)),
            ["time"] = new(new("Fight time", "Seconds since the fight started."),
                (_, ctx) => ctx.CurrentTime.TotalSeconds),
            ["gcd"] = new(new("Global cooldown", "The length of the global cooldown, in seconds. The value is the base global cooldown of the hero divided by (1 + haste). Example: a base of 1.5 at 25% haste gives 1.2. The value is the full length. It is not the time that remains on the current global cooldown."),
                (hero, ctx) => Gcd(hero, ctx.CurrentTime)),
            ["gcd.max"] = new(new("Global cooldown (max)", "The same value as gcd: the length of the global cooldown, in seconds."),
                (hero, ctx) => Gcd(hero, ctx.CurrentTime)),
            ["dungeon_event.adds.exists"] = new(new("Trash pull", "1 while a dungeon enemy that is not a boss is in combat. Otherwise 0."),
                (_, ctx) => ctx.Trash ? 1.0 : 0.0),
            ["dungeon_event.adds.count"] = new(new("Trash enemies", "The number of dungeon enemies in combat that are not bosses. 0 if there are none."),
                (_, ctx) => ctx.Trash ? CountActive(ctx, boss: false) : 0.0),
            ["dungeon_event.adds.in"] = new(new("Next trash pull in", $"Seconds until the next pull of a dungeon with enemies that are not bosses starts, an estimate from the pulls so far. {NeverIn} if no such pull follows, or before damage gives an estimate."),
                (_, ctx) => SecondsUntil(ctx, ctx.NextTrashAt)),
            ["dungeon_event.boss.exists"] = new(new("Boss fight", "1 while a dungeon boss is in combat, or in a training dummy fight against a boss. Otherwise 0. A boss that is not the final boss is dungeon_event.boss.exists & !dungeon_event.boss.final."),
                (_, ctx) => ctx.Boss ? 1.0 : 0.0),
            ["dungeon_event.boss.count"] = new(new("Bosses", "The number of bosses in combat. A council of three bosses gives 3."),
                (_, ctx) => CountActive(ctx, boss: true)),
            ["dungeon_event.boss.final"] = new(new("Final boss", "1 while the final boss of a dungeon is in combat. Otherwise 0."),
                (_, ctx) => ctx.FinalBoss ? 1.0 : 0.0),
            ["dungeon_event.boss.in"] = new(new("Next boss in", $"Seconds until the next pull of a dungeon with a boss starts, an estimate from the pulls so far. {NeverIn} if no boss follows, or before damage gives an estimate."),
                (_, ctx) => SecondsUntil(ctx, ctx.NextBossAt)),
            ["dungeon_event.fight.remains"] = new(new("Fight time remaining", "Seconds until the fight ends. In a dungeon, an estimate from the pulls so far, at most the time to the time limit."),
                (_, ctx) => SecondsUntil(ctx, ctx.FightEnd)),
        };

    /// <summary>The hasted global cooldown, as <see cref="SimulationContext.BeginCast"/> sets it.</summary>
    public static double Gcd(Hero hero, SimTime now) => hero.GlobalCooldownSeconds / (1.0 + hero.GetHaste(now));

    /// <summary>Properties of an aura on the hero (buff) or an effect on the current target (debuff); null when it is not up.</summary>
    private static readonly Dictionary<string, AplDescribed<Func<TargetEffect?, SimTime, double>>> AuraProperties = new()
    {
        ["up"] = new(new("Up", "1 if the aura is up. Otherwise 0."), (aura, _) => aura != null ? 1.0 : 0.0),
        // SimulationCraft's word for a damage over time effect that is up.
        ["ticking"] = new(new("Ticking", "The same value as up: 1 if the aura is up. Otherwise 0."), (aura, _) => aura != null ? 1.0 : 0.0),
        ["down"] = new(new("Down", "1 if the aura is down. Otherwise 0."), (aura, _) => aura == null ? 1.0 : 0.0),
        ["stack"] = new(new("Stacks", "The number of stacks of the aura. 0 if the aura is down."), (aura, _) => aura?.Stacks ?? 0),
        // SimulationCraft's react is the stacks the player has seen; the sim reacts at once.
        ["react"] = new(new("Stacks (react)", "The same value as stack: the number of stacks of the aura. 0 if the aura is down."), (aura, _) => aura?.Stacks ?? 0),
        ["at_max_stacks"] = new(new("At max stacks", "1 if the aura has its maximum number of stacks. Otherwise 0."),
            (aura, _) => aura is { } a && a.Stacks >= a.MaxStacks ? 1.0 : 0.0),
        ["remains"] = new(new("Time remaining", "Seconds until the aura expires. 0 if the aura is down. For an aura that does not expire, the value is a very large number."),
            (aura, now) => aura is not { } a ? 0.0 : a.ExpiresAt == SimTime.Infinite ? double.MaxValue : (a.ExpiresAt - now).TotalSeconds),
        ["refreshable"] = new(new("Refreshable", $"1 if the aura is down or in the last {RefreshableFraction * 100}% of its duration. Otherwise 0."),
            (aura, now) => IsRefreshable(aura, now) ? 1.0 : 0.0),
    };

    /// <summary>True when <paramref name="aura"/> is down or in the last <see cref="RefreshableFraction"/> of its duration.</summary>
    public static bool IsRefreshable(TargetEffect? aura, SimTime now) =>
        aura is not { } a || (a.ExpiresAt != SimTime.Infinite && (a.ExpiresAt - now).TotalSeconds < RefreshableFraction * a.Duration.TotalSeconds);

    /// <summary>Properties of an ability's cooldown, by the ability's name.</summary>
    private static readonly Dictionary<string, AplDescribed<Func<Hero, Ability?, string, SimTime, double>>> CooldownProperties = new()
    {
        ["ready"] = new(new("Ready", "1 if the ability has a charge ready. Otherwise 0."),
            (hero, _, name, now) => hero.IsCooldownReady(name, now) ? 1.0 : 0.0),
        ["up"] = new(new("Ready (up)", "The same value as ready: 1 if the ability has a charge ready. Otherwise 0."),
            (hero, _, name, now) => hero.IsCooldownReady(name, now) ? 1.0 : 0.0),
        ["remains"] = new(new("Time until ready", "Seconds until the ability has a charge ready. 0 if a charge is ready now."),
            (hero, _, name, now) => hero.GetCooldownRemains(name, now)),
        ["charges"] = new(new("Charges", "The number of charges that are ready now."),
            (hero, ability, name, now) => hero.Cooldowns.TryGetValue(name, out var state) ? state.ChargesAt(now) : ability?.MaxCharges ?? 1),
        ["duration"] = new(new("Cooldown", "The base cooldown of the ability, in seconds."),
            (_, ability, _, _) => ability?.Cooldown.TotalSeconds ?? 0.0),
        ["full_recharge_time"] = new(new("Time to full charges",
                "Seconds until all charges of the ability are ready. The value is the time that remains on the charge that recharges now, plus a full cooldown for each other missing charge. 0 if all charges are ready."),
            (hero, ability, name, now) => hero.Cooldowns.TryGetValue(name, out var state) ? FullRechargeTime(state, ability, now) : 0.0),
        ["charges_fractional"] = new(new("Charges (fractional)",
                "The number of charges that are ready, plus the recharged part of the next charge. Example: 1.25 is a ready charge and a quarter of the next charge. If all charges are ready, the value is equal to charges."),
            // The charges held and the part of the next one recharged so far (2.25: two, and a quarter of the third).
            (hero, ability, name, now) =>
                hero.Cooldowns.TryGetValue(name, out var state) ? state.ChargesAt(now) + state.RechargedPart(now, now) : ability?.MaxCharges ?? 1),
    };

    /// <summary>
    /// Seconds until every charge is back: the charge recharging now, then a full cooldown for each other missing charge.
    /// </summary>
    public static double FullRechargeTime(CooldownState state, Ability? ability, SimTime now)
    {
        int missing = state.MaxCharges - state.ChargesAt(now);
        if (missing <= 0) return 0.0;
        return Math.Max(0.0, (state.NextChargeAt - now).TotalSeconds) + (missing - 1) * (ability?.Cooldown.TotalSeconds ?? 0.0);
    }

    /// <summary>Properties of an action's ability, by the ability's name.</summary>
    private static readonly Dictionary<string, AplDescribed<Func<Ability?, SimTime, double>>> ActionProperties = new()
    {
        // An instant cast takes the global cooldown.
        ["execute_time"] = new(new("Cast time", "The hasted cast time, in seconds. For an instant ability, the value is the hasted global cooldown."),
            (ability, now) => ability == null ? 0.0 : ability.IsInstant ? Gcd(ability.Owner, now) : ability.GetAdjustedCastTime(now).TotalSeconds),
    };

    /// <summary>Ticks of a channel: <c>channel.&lt;ability&gt;.ticks</c>.</summary>
    public const string ChannelTicksProperty = "ticks";
    /// <summary>Seconds since a channel started: <c>channel.&lt;ability&gt;.after_sec</c>.</summary>
    public const string ChannelSecondsProperty = "after_sec";
    /// <summary>Part of a channel's duration gone, in percent: <c>channel.&lt;ability&gt;.after_pct</c>.</summary>
    public const string ChannelPercentProperty = "after_pct";

    /// <summary>
    /// Properties of the hero's running channel, by its ability's name, at a time: each reads 0 when no channel of that
    /// ability runs.
    /// </summary>
    private static readonly Dictionary<string, AplDescribed<Func<ActiveChannel, SimTime, double>>> ChannelProperties = new()
    {
        [ChannelTicksProperty] = new(new("Channel ticks", "The number of ticks of the channel so far. 0 if the channel of the ability does not run."),
            (channel, _) => channel.Ticks.TicksFired),
        [ChannelSecondsProperty] = new(new("Channel time", "Seconds since the channel started, after the cast time of the ability. 0 if the channel of the ability does not run."),
            (channel, time) => (time - channel.Ticks.Start).TotalSeconds),
        [ChannelPercentProperty] = new(new("Channel time %", "The part of the channel duration that has gone, in percent. 0 if the channel of the ability does not run."),
            (channel, time) => ChannelPercent(channel, time)),
    };

    /// <summary>The part of <paramref name="channel"/>'s duration gone at <paramref name="time"/>, in percent.</summary>
    public static double ChannelPercent(ActiveChannel channel, SimTime time) =>
        100.0 * (time - channel.Ticks.Start).TotalSeconds / (channel.Ticks.End - channel.Ticks.Start).TotalSeconds;

    /// <summary>The hero's running channel when it is <paramref name="ability"/>'s; else null.</summary>
    public static ActiveChannel? ChannelOf(Hero hero, Ability? ability) =>
        ability != null && hero.CurrentChannel is { } channel && channel.Ability == ability ? channel : null;

    /// <summary>The value of a channel property ("ticks") at <paramref name="time"/>; 0 when no channel of <paramref name="ability"/> runs, or for a property channels lack.</summary>
    public static double ChannelProperty(Hero hero, Ability? ability, string property, SimTime time) =>
        ChannelOf(hero, ability) is { } channel && ChannelProperties.TryGetValue(property, out var read) ? read.Value(channel, time) : 0.0;

    /// <summary>The scopes of "&lt;scope&gt;.&lt;name&gt;.&lt;property&gt;" paths and the properties each one has.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyCollection<string>> Scopes { get; } =
        new Dictionary<string, IReadOnlyCollection<string>>
        {
            [BuffScope] = AuraProperties.Keys,
            [DebuffScope] = DebuffProperties,
            [DotScope] = DebuffProperties,
            [CooldownScope] = CooldownProperties.Keys,
            [ActionScope] = ActionProperties.Keys,
            [ChannelScope] = ChannelProperties.Keys,
        };

    /// <summary>
    /// The scopes of "&lt;scope&gt;.&lt;name&gt;" paths that read the hero's build: 1 when the hero has a talent, blessing,
    /// legendary or weapon trait of that name, or a weapon (or weapon ability) whose name starts with it. They do not change
    /// during a fight.
    /// </summary>
    public static IReadOnlyDictionary<string, AplDescribed<Func<Hero, string, bool>>> BuildScopes { get; } =
        new Dictionary<string, AplDescribed<Func<Hero, string, bool>>>
        {
            [TalentScope] = new(new("Talent", "1 if the build has the talent. Otherwise 0."),
                (hero, name) => hero.Talents.Any(t => AplNames.Slug(t.Name) == name)),
            [BlessingScope] = new(new("Blessing", "1 if the build has the blessing. Otherwise 0."),
                (hero, name) => hero.Glyphs.Any(b => AplNames.Slug(b.Name) == name)),
            [LegendaryScope] = new(new("Legendary", "1 if the build has the legendary. Otherwise 0."),
                (hero, name) => hero.Legendaries.Any(l => AplNames.Slug(l.Name) == name)),
            [WeaponTraitScope] = new(new("Weapon trait", "1 if the build has the weapon trait. Otherwise 0."),
                (hero, name) => hero.Augmentations.Any(t => AplNames.Slug(t.Name) == name)),
            [WeaponScope] = new(new("Weapon", "1 if the name of the equipped weapon, or of its weapon ability, starts with this name. Otherwise 0."),
                (hero, name) =>
                    (hero.EquippedWeapon != null && AplNames.Slug(hero.EquippedWeapon.Name).StartsWith(name, StringComparison.Ordinal))
                    || (hero.WeaponAbility != null && AplNames.Slug(hero.WeaponAbility.Name).StartsWith(name, StringComparison.Ordinal))),
        };

    /// <summary>Each scope of <see cref="Scopes"/>, <see cref="ActiveDotScope"/> and <see cref="VariableScope"/>, with what its paths read.</summary>
    public static IReadOnlyDictionary<string, AplDoc> ScopeDocs { get; } = new Dictionary<string, AplDoc>
    {
        [BuffScope] = new("Buff", "An aura on the hero."),
        [DebuffScope] = new("Debuff on target", "An aura or effect from the hero on the current target."),
        [DotScope] = new("Dot on target", "The same as debuff: an aura or effect from the hero on the current target."),
        [CooldownScope] = new("Cooldown", "The cooldown and charges of an ability."),
        [ActionScope] = new("Action", "The cast of an ability."),
        [ChannelScope] = new("Channel", "The channel of an ability while it runs."),
        [ActiveDotScope] = new("Enemies with debuff", "The number of enemies in combat that have the debuff."),
        [VariableScope] = new("Variable", "The value that a variable action set in this fight. Before a variable action sets it, the value is the default of the variable."),
        [PrevScope] = new("Previous action", "1 if the last ability the hero started to cast is this ability. Otherwise 0. 0 before the first cast of the fight."),
        [PrevGcdScope] = new("Previous GCD action", "1 if the last ability the hero started to cast on the global cooldown is this ability. Otherwise 0. A cast off the global cooldown leaves it as it is. 0 before the first cast on the global cooldown."),
    };

    /// <summary>A resource's current amount, written as its name alone ("winter_orbs") or after <see cref="ResourcePrefix"/>.</summary>
    public static AplDoc ResourceCurrent { get; } = new("Current", "The current amount of the resource.");

    /// <summary>The room left in a resource, written with <see cref="DeficitSuffix"/>.</summary>
    public static AplDoc ResourceDeficit { get; } = new("Deficit", "The maximum amount of the resource minus its current amount.");

    /// <summary>The property's doc in a scope of <see cref="Scopes"/>; null for a scope or property not in it.</summary>
    public static AplDoc? PropertyDoc(string scope, string property) => scope switch
    {
        BuffScope => AuraProperties.GetValueOrDefault(property)?.Doc,
        DebuffScope or DotScope => PickedDoc(property) ?? AuraProperties.GetValueOrDefault(property)?.Doc,
        CooldownScope => CooldownProperties.GetValueOrDefault(property)?.Doc,
        ActionScope => ActionProperties.GetValueOrDefault(property)?.Doc,
        ChannelScope => ChannelProperties.GetValueOrDefault(property)?.Doc,
        _ => null,
    };

    /// <summary>The doc of a picked application's property ("oldest.remains"); null for any other property.</summary>
    private static AplDoc? PickedDoc(string property)
    {
        var parts = property.Split('.');
        if (parts.Length != 2 || PickOf(parts[0]) is not { } pick || !PickedProperties.Contains(parts[1])) return null;
        string which = pick == TargetEffectPick.Oldest ? "oldest" : "newest";
        string applied = pick == TargetEffectPick.Oldest ? "first" : "last";
        var doc = AuraProperties[parts[1]].Doc;
        return new AplDoc($"{doc.Label} ({which})",
            $"{doc.Description} The value reads the application applied {applied}, for a debuff whose every application runs on its own (Engulfing Flames). For any other debuff, the same value as {parts[1]}.");
    }

    /// <summary>The application <paramref name="word"/> picks ("oldest"); null for any other word.</summary>
    public static TargetEffectPick? PickOf(string word) => word switch
    {
        OldestPick => TargetEffectPick.Oldest,
        NewestPick => TargetEffectPick.Newest,
        _ => null,
    };

    /// <summary>
    /// The doc of an action's own value in <see cref="ActionValues"/>: the label of the property it stands for, and that
    /// property's meaning on the action's ability. Null for any other path.
    /// </summary>
    public static AplDoc? ActionValueDoc(string path)
    {
        if (OfAction(path, ActionAbility) is not { } scoped) return null;
        var parts = new PropertyExpression(scoped);
        return PropertyDoc(parts.Scope!, parts.Property!) is { } doc ? new AplDoc(doc.Label, $"The same value as {scoped}. {doc.Description}") : null;
    }

    /// <summary>The ability's name in <see cref="ActionValueDoc"/>: the action's own.</summary>
    private const string ActionAbility = "<this action>";

    public string PropertyPath { get; }

    /// <summary>
    /// The scope of a "&lt;scope&gt;.&lt;name&gt;.&lt;property&gt;" path, of a "&lt;scope&gt;.&lt;name&gt;" path in
    /// <see cref="BuildScopes"/>, or <see cref="VariableScope"/>; else null.
    /// </summary>
    public string? Scope { get; }
    /// <summary>The aura, ability, build part or variable a scoped path names ("glacial_assault").</summary>
    public string? Name { get; }
    /// <summary>The property a scoped path reads ("stack"); null for a build part or a variable. A property its scope lacks reads as "up" or "ready".</summary>
    public string? Property { get; }
    /// <summary>The application a debuff path reads (<see cref="OldestPick"/>, <see cref="NewestPick"/>); every application as one when not written.</summary>
    public TargetEffectPick Pick { get; }
    /// <summary>
    /// The aggregate over every enemy in combat written before the path and a ':' (<c>min:dot.engulfing_flames.remains</c>);
    /// null for a path that reads the current target. It reads a debuff's <see cref="AplEnemyAggregates.Property"/> alone
    /// (<see cref="IsEnemyAggregate"/>).
    /// </summary>
    public AplEnemyAggregate? Aggregate { get; }

    /// <summary>True for an <see cref="Aggregate"/> of a debuff's remaining time with no oldest or newest pick.</summary>
    public bool IsEnemyAggregate => Aggregate != null && Scope == DebuffScope && Property == AplEnemyAggregates.Property && Pick == TargetEffectPick.All;

    public PropertyExpression(string propertyPath)
    {
        string path = propertyPath.Trim().ToLowerInvariant();
        int colon = path.IndexOf(':');
        if (colon > 0 && AplEnemyAggregates.ByPrefix.TryGetValue(path[..colon], out var aggregate))
        {
            Aggregate = aggregate;
            path = path[(colon + 1)..].Trim();
        }
        path = AplPaths.Migrate(path);
        PropertyPath = Aggregate is { } prefix ? $"{prefix.Prefix()}:{path}" : path;
        var parts = path.Split('.');
        if (parts.Length == 4 && parts[0] is DotScope or DebuffScope && PickOf(parts[2]) is { } pick)
        {
            (Scope, Name, Pick, Property) = (DebuffScope, parts[1], pick, parts[3]);
        }
        else if (parts.Length >= 3 && parts[0] == DotScope)
        {
            (Scope, Name, Property) = (DebuffScope, parts[1], parts[2]);
        }
        else if (parts.Length >= 3 && Scopes.ContainsKey(parts[0]))
        {
            (Scope, Name, Property) = (parts[0], parts[1], parts[2]);
        }
        else if (parts.Length == 2 && (BuildScopes.ContainsKey(parts[0]) || parts[0] is VariableScope or ActiveDotScope or PrevScope or PrevGcdScope))
        {
            (Scope, Name) = (parts[0], parts[1]);
        }
    }

    /// <summary>The resource name of an unscoped path ("winter_orbs" for "resource.winter_orbs" and "winter_orbs.deficit").</summary>
    public string ResourceName
    {
        get
        {
            string path = PropertyPath.StartsWith(ResourcePrefix) ? PropertyPath[ResourcePrefix.Length..] : PropertyPath;
            return IsDeficit ? path[..^DeficitSuffix.Length] : path;
        }
    }

    /// <summary>True for a path that reads the room left in a resource ("spirit.deficit").</summary>
    public bool IsDeficit => PropertyPath.EndsWith(DeficitSuffix, StringComparison.Ordinal);

    public bool Evaluate(Hero hero, SimulationContext ctx)
    {
        return EvaluateNumeric(hero, ctx) != 0.0;
    }

    /// <remarks>A variable reads 0 here: only a bound program (<see cref="AplProgram"/>) runs variable actions.</remarks>
    public double EvaluateNumeric(Hero hero, SimulationContext ctx)
    {
        var now = ctx.CurrentTime;

        if (Aggregate is { } aggregate) return IsEnemyAggregate ? AcrossEnemies(hero, ctx, Name!, aggregate) : 0.0;

        if (Named.TryGetValue(PropertyPath, out var named)) return named.Value(hero, ctx);

        if (hero.FindResource(ResourceName) is { } resource)
        {
            var state = hero.Resources.Get(resource);
            return IsDeficit ? state.Max - state.Current : state.Current;
        }

        switch (Scope)
        {
            case BuffScope:
                return AuraProperty(Property!)(AuraLookup.Find(hero.Auras, Name!, now)?.Seen(now, ctx.ProcReactionTime), now);

            case DebuffScope:
                var target = ctx.Player.CurrentTarget;
                return AuraProperty(Property!)(target == null ? null : AuraLookup.On(hero, target, Name!, now, Pick), now);

            case PrevScope:
                return ctx.PreviousCast is { } previous && AplNames.NamesAbility(Name!, previous) ? 1.0 : 0.0;

            case PrevGcdScope:
                return ctx.PreviousGcdCast is { } previousGcd && AplNames.NamesAbility(Name!, previousGcd) ? 1.0 : 0.0;

            case ActiveDotScope:
                return ctx.ActiveEnemies.Count(enemy => AuraLookup.On(hero, enemy, Name!, now) != null);

            case CooldownScope:
                // Cooldowns are kept under the ability's own name ("Cold Snap"), not its action-list name.
                var ability = AplNames.FindAbility(hero, Name!);
                var cooldown = CooldownProperty(Property!);
                return cooldown(hero, ability, ability?.Name ?? Name!, now);

            case ActionScope:
                return ActionProperty(Property!)(AplNames.FindAbility(hero, Name!), now);

            case ChannelScope:
                return ChannelProperty(hero, AplNames.FindAbility(hero, Name!), Property!, now);

            case { } scope when BuildScopes.TryGetValue(scope, out var has):
                return has.Value(hero, Name!) ? 1.0 : 0.0;
        }

        return 0.0;
    }

    /// <summary>
    /// <paramref name="aggregate"/> of the remaining time of debuff <paramref name="slug"/> over every application on every
    /// enemy in combat; <see cref="AplEnemyAggregate"/> has the value when no enemy has it.
    /// </summary>
    internal static double AcrossEnemies(Hero hero, SimulationContext ctx, string slug, AplEnemyAggregate aggregate) =>
        RemainsAcross(AuraLookup.ExpiryAcross(hero, ctx, slug, aggregate, ctx.CurrentTime), aggregate, ctx.CurrentTime);

    /// <summary>The value of an aggregate whose application expires at <paramref name="expiresAt"/>, null for none.</summary>
    internal static double RemainsAcross(SimTime? expiresAt, AplEnemyAggregate aggregate, SimTime time) => expiresAt switch
    {
        null => aggregate == AplEnemyAggregate.Min ? double.MaxValue : 0.0,
        { } e when e == SimTime.Infinite => double.MaxValue,
        { } e => (e - time).TotalSeconds,
    };

    /// <summary>The value of an action property ("execute_time"); a property actions lack reads as 0.</summary>
    public static Func<Ability?, SimTime, double> ActionProperty(string property) =>
        ActionProperties.GetValueOrDefault(property)?.Value ?? ((_, _) => 0.0);

    /// <summary>The value of a cooldown property; a property cooldowns lack reads as "ready".</summary>
    public static Func<Hero, Ability?, string, SimTime, double> CooldownProperty(string property) =>
        CooldownProperties.GetValueOrDefault(property)?.Value ?? CooldownProperties["ready"].Value;

    /// <summary>
    /// Paths an action reads about its own ability, unscoped ("refreshable", "full_recharge_time"), as the scoped path
    /// they stand for on the action's ability <paramref name="ability"/>; null for any other path.
    /// </summary>
    public static string? OfAction(string path, string ability) =>
        ActionValues.TryGetValue(path, out var scoped) ? scoped(ability) : null;

    /// <summary>Paths an action reads about its own ability, with the scoped path each stands for on an ability.</summary>
    public static IReadOnlyDictionary<string, Func<string, string>> ActionValues { get; } = new Dictionary<string, Func<string, string>>
    {
        ["refreshable"] = ability => $"{DebuffScope}.{ability}.refreshable",
        ["ticking"] = ability => $"{DebuffScope}.{ability}.ticking",
        ["full_recharge_time"] = ability => $"{CooldownScope}.{ability}.full_recharge_time",
        ["charges"] = ability => $"{CooldownScope}.{ability}.charges",
        ["charges_fractional"] = ability => $"{CooldownScope}.{ability}.charges_fractional",
        ["execute_time"] = ability => $"{ActionScope}.{ability}.execute_time",
        ["cast_time"] = ability => $"{ActionScope}.{ability}.execute_time",
        [ChannelTicksProperty] = ability => $"{ChannelScope}.{ability}.{ChannelTicksProperty}",
        [ChannelSecondsProperty] = ability => $"{ChannelScope}.{ability}.{ChannelSecondsProperty}",
        [ChannelPercentProperty] = ability => $"{ChannelScope}.{ability}.{ChannelPercentProperty}",
    };

    private static Func<TargetEffect?, SimTime, double> AuraProperty(string property) =>
        AuraProperties.GetValueOrDefault(property)?.Value ?? AuraProperties["up"].Value;

    public override string ToString() => PropertyPath;
}

public class UnaryOpExpression : IAplExpression
{
    public UnaryOperator Operator { get; }
    public IAplExpression Operand { get; }

    public UnaryOpExpression(UnaryOperator op, IAplExpression operand)
    {
        Operator = op;
        Operand = operand;
    }

    public bool Evaluate(Hero hero, SimulationContext ctx)
    {
        return Operator switch
        {
            UnaryOperator.Not => !Operand.Evaluate(hero, ctx),
            UnaryOperator.Minus => Operand.EvaluateNumeric(hero, ctx) != 0.0,
            _ => throw new InvalidOperationException($"Unsupported APL unary operator: '{Operator}'")
        };
    }

    public double EvaluateNumeric(Hero hero, SimulationContext ctx)
    {
        return Operator switch
        {
            UnaryOperator.Not => Operand.Evaluate(hero, ctx) ? 0.0 : 1.0,
            UnaryOperator.Minus => -Operand.EvaluateNumeric(hero, ctx),
            _ => throw new InvalidOperationException($"Unsupported APL unary operator: '{Operator}'")
        };
    }

    public override string ToString() => $"{Operator.Symbol()}({Operand})";
}

/// <summary>An expression written in parentheses. <see cref="AplFormatter"/> writes the parentheses back.</summary>
public class GroupExpression(IAplExpression inner) : IAplExpression
{
    public IAplExpression Inner { get; } = inner;

    public bool Evaluate(Hero hero, SimulationContext ctx) => Inner.Evaluate(hero, ctx);

    public double EvaluateNumeric(Hero hero, SimulationContext ctx) => Inner.EvaluateNumeric(hero, ctx);

    public override string ToString() => Inner.ToString()!;
}

public class BinaryOpExpression : IAplExpression
{
    public IAplExpression Left { get; init; }
    public IAplExpression Right { get; init; }
    public BinaryOperator Operator { get; init; }

    public BinaryOpExpression(IAplExpression left, BinaryOperator op, IAplExpression right)
    {
        Left = left;
        Operator = op;
        Right = right;
    }

    public bool Evaluate(Hero hero, SimulationContext ctx)
    {
        return Operator switch
        {
            BinaryOperator.And => Left.Evaluate(hero, ctx) && Right.Evaluate(hero, ctx),
            BinaryOperator.Or => Left.Evaluate(hero, ctx) || Right.Evaluate(hero, ctx),
            BinaryOperator.Less => Left.EvaluateNumeric(hero, ctx) < Right.EvaluateNumeric(hero, ctx),
            BinaryOperator.LessEqual => Left.EvaluateNumeric(hero, ctx) <= Right.EvaluateNumeric(hero, ctx),
            BinaryOperator.Greater => Left.EvaluateNumeric(hero, ctx) > Right.EvaluateNumeric(hero, ctx),
            BinaryOperator.GreaterEqual => Left.EvaluateNumeric(hero, ctx) >= Right.EvaluateNumeric(hero, ctx),
            BinaryOperator.Equal => Math.Abs(Left.EvaluateNumeric(hero, ctx) - Right.EvaluateNumeric(hero, ctx)) < 0.0001,
            BinaryOperator.NotEqual => Math.Abs(Left.EvaluateNumeric(hero, ctx) - Right.EvaluateNumeric(hero, ctx)) >= 0.0001,
            BinaryOperator.Add or BinaryOperator.Subtract or BinaryOperator.Multiply or BinaryOperator.Divide or BinaryOperator.Modulo =>
                EvaluateNumeric(hero, ctx) != 0.0,
            _ => throw new InvalidOperationException($"Unsupported APL binary operator: '{Operator}'")
        };
    }

    public double EvaluateNumeric(Hero hero, SimulationContext ctx)
    {
        switch (Operator)
        {
            case BinaryOperator.Add or BinaryOperator.Subtract or BinaryOperator.Multiply or BinaryOperator.Divide or BinaryOperator.Modulo:
                double left = Left.EvaluateNumeric(hero, ctx);
                double right = Right.EvaluateNumeric(hero, ctx);
                return Operator switch
                {
                    BinaryOperator.Add => left + right,
                    BinaryOperator.Subtract => left - right,
                    BinaryOperator.Multiply => left * right,
                    BinaryOperator.Divide => right == 0.0 ? 0.0 : left / right,
                    _ => right == 0.0 ? 0.0 : left % right
                };
            default:
                return Evaluate(hero, ctx) ? 1.0 : 0.0;
        }
    }

    public override string ToString() => $"({Left} {Operator.Symbol()} {Right})";
}

internal static class AuraLookup
{
    /// <summary>An active aura whose name, as an APL slug ("glacial_assault"), matches <paramref name="slug"/>.</summary>
    public static Aura? Find(AuraContainer auras, string slug, SimTime now) => auras.FindActive(slug, now);

    /// <summary>
    /// The effect <paramref name="slug"/> <paramref name="hero"/> keeps on <paramref name="enemy"/>
    /// (<see cref="ITargetEffects"/>: its own expiry, where the enemy's debuff that shows it has none), or else the aura of
    /// that name on it; null when neither is on it. An aura has one application, so <paramref name="pick"/> reads it the same.
    /// </summary>
    public static TargetEffect? On(Hero hero, EnemyTarget enemy, string slug, SimTime now, TargetEffectPick pick = TargetEffectPick.All) =>
        hero is ITargetEffects effects && effects.FindOn(enemy, slug, now, pick) is { } effect ? effect
        : enemy.Auras.FindActive(slug, now) is { } aura ? TargetEffect.Of(aura) : null;

    /// <summary><see cref="On(Hero, EnemyTarget, string, SimTime, TargetEffectPick)"/>, with the enemy's auras searched by <paramref name="search"/>.</summary>
    public static TargetEffect? On(Hero hero, EnemyTarget enemy, string slug, SimTime now, TargetEffectPick pick, ref AuraSearch search) =>
        hero is ITargetEffects effects && effects.FindOn(enemy, slug, now, pick) is { } effect ? effect
        : search.Find(enemy.Auras, slug, now) is { } aura ? TargetEffect.Of(aura) : null;

    /// <summary>
    /// The expiry of the application of <paramref name="slug"/> that runs out first (<see cref="AplEnemyAggregate.Min"/>) or
    /// last (<see cref="AplEnemyAggregate.Max"/>) over every enemy in combat; null when no enemy has it.
    /// </summary>
    public static SimTime? ExpiryAcross(Hero hero, SimulationContext ctx, string slug, AplEnemyAggregate aggregate, SimTime now)
    {
        var pick = aggregate == AplEnemyAggregate.Min ? TargetEffectPick.ExpiresFirst : TargetEffectPick.All;
        SimTime? found = null;
        foreach (var enemy in ctx.ActiveEnemies)
        {
            if (On(hero, enemy, slug, now, pick) is not { } effect) continue;
            if (found is not { } best || (aggregate == AplEnemyAggregate.Min ? effect.ExpiresAt < best : effect.ExpiresAt > best)) found = effect.ExpiresAt;
        }
        return found;
    }
}
