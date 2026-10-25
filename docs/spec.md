# Exact replay: log values and the replay verifier

Exact replay (`ReplayDriver` with `LogOutcomeSource`, `ReplayOptions.FreeDiceSeed` null) executes every logged event
with the log's values. Each crit, proc, target, landing time, damage amount, tick time and cast time is the log's. The
replay makes no random roll.

`ReplayVerifier` compares each logged value with the range the engine computes for it. Each value outside its range is a
`ReplayIssue` with the expected range, the logged value, the deviation and the engine state at that moment.

## Replay

### Outcomes from the log

| Outcome | Source in exact replay |
|---|---|
| Crit | The matched log hit's `Crit` |
| Proc | `RollProc` succeeds when `ProcEvidence` finds the proc's evidence in the log at the roll |
| Targets | `SelectTargets` and `ChooseTarget` answer from `HitLedger` |
| Landing time | The matched log hit's `Time` |
| Damage | The matched log hit's `UnblockedAmount` |
| Periodic tick time | The time of the log tick `HitLedger` reserves for it |
| Cast start, completion, cancel and channel stop | The command's `Time` |

Every outcome comes from `LogOutcomeSource`. Every hit calls `IOutcomeSource.ResolveHit` and `IOutcomeSource.Land`,
including a hit with `ForcedCrit`: `HitRequest.ForcedCrit` has the forced crit, `RandomOutcomeSource` uses it as the
crit, and `ReplayVerifier` compares it with the log hit's crit.

### Damage dealt

- An engine hit matched to a log hit deals the log hit's `UnblockedAmount`. Target health, `TotalDamage`,
  `Hero.OnDamageDealt` and every subscriber read that amount. Pools built from `DamageContext.Amount` (Fire Frog's dot,
  Crackling Inferno, Flare Up, Amethyst Splinters) therefore start from the logged damage.
- An engine hit with no log hit returns `HitOutcome(Missed: true)` from `ResolveHit`. It deals no damage and raises no
  `Hero.OnDamageDealt`; its `onLanded` handler runs with 0. `ResolveHit` records its `HitPair` with the expected range
  and `EngineState` at release, from `hit.BaseDamage`, and `ReplayVerifier` flags it.
- A hit on a target that dies in the log before the hit could land (`MissHitsOnDyingTargets`) also returns
  `Missed: true`. It counts in `LogOutcomeSource.MissedHits` and raises no issue.
- A log hit no engine hit matched is dealt by the replay through `SimulationContext.DealLoggedHit` at
  `Time + ReplayOptions.HitEarlySeconds`. That moment ends the window in which a later engine release can still match
  it. `ReplayVerifier` flags it. The hit raises `Hero.OnDamageDealt` with the log's damage ID, target, amount and crit.
- Log hits of an ability the engine does not model are dealt the same way. The step stays `Modeled = false`.

### Casts

- Every logged cast is made. A cast the engine refuses (cooldown, resources, global cooldown, still casting) is made
  with `force: true`, added to `ReplayResult.ForcedCasts`, and flagged by `ReplayVerifier` with the refusal's range.
- A `begin_cast` completes at its `complete_cast` time and cancels at its `cancel_cast` time.
- A channel stops at its `stop_channel` time.

### Periodic ticks

`PeriodicEffect.Begin` and `PeriodicEffect.BeginHasted` take an optional damage log ID and target. An effect with a
damage log ID fires each tick at a log tick's time:

- Each tick asks `IOutcomeSource.TickAt` for its firing time. The request has the engine's scheduled time and the tick's
  fraction. Each tick asks once the previous tick has fired, so one missing log tick shifts no later pairing.
- `LogOutcomeSource.TickAt` reserves the unclaimed log hit with that damage ID on the effect's target landing nearest
  the scheduled time, within half the effect's period on either side, and returns its time. The reservation goes into
  `HitLedger.Reserve`, so the tick's own hit takes it in `HitLedger.Match`.
- An effect without a single target (a pulse on every enemy in its area) reserves a `ClaimGroup` group the same way.
  `SelectTargets` called from inside that tick returns the reserved group's targets.
- Each reservation belongs to one `PeriodicEffect`. Two applications of one damage ID on one target therefore keep
  their own log ticks: a log tick reserved by one effect is not free for the other.
- `PeriodicEffect.Stop` and `Finish` release the effect's reservation that has not fired.
- For `BeginHasted`, the `HastedTimer` starts at the previous log tick and runs to its own fire time. That fire records
  the engine's predicted tick time for the verifier and fires no tick. Each interval is therefore checked on its own.
- For `Begin`, the predicted tick time is the previous log tick plus the period.
- A tick with no log tick in its window fires at the engine's scheduled time. Its hits are engine hits with no log hit.
- The partial final tick keeps its engine fraction for `HitPair.TickFraction` and deals the log amount.

An effect without a damage log ID (resource gains) fires on the engine's schedule.

## Core interface

`IOutcomeSource` in FellowshipSim.Core changes in two members:

```csharp
/// <summary>The hit lands: returns the damage it deals.</summary>
double Land(in HitRequest landed, HitOutcome outcome, SimulationContext ctx);

/// <summary>Firing time of a periodic tick with a damage log ID.</summary>
SimTime TickAt(in TickRequest tick, SimulationContext ctx);
```

```csharp
public readonly record struct TickRequest(
    Hero Source,
    EnemyTarget? Target,
    int DamageLogId,
    string Name,
    SimTime Scheduled,
    double Fraction);
```

- `RandomOutcomeSource.Land` returns `landed.BaseDamage` times the crit and spread multiplier. `RandomOutcomeSource.TickAt`
  returns `tick.Scheduled`.
- `SimulationContext.Land` deals the amount `Land` returns.
- `SimulationContext.DealLoggedHit(Hero source, EnemyTarget target, int abilityId, string abilityName, double amount, bool isCrit)`
  deals a fixed amount with no `IOutcomeSource` call.
- `HitRequest` gains `bool? ForcedCrit`.
- Monte Carlo keeps zero allocation per hit and per tick, and gives the same totals for each seed as commit 8fecd72.

## ReplayVerifier

`Replay/ReplayVerifier.cs` in FellowshipSim.Engine. `LogOutcomeSource` and `ReplayDriver` pass it each engine value and
its log counterpart. The verifier has every comparison and every tolerance, and reports each disagreement as a
`ReplayIssue`. `ReplayDriver.CheckState` moves into it.

In every check the expected range comes from the engine and the observed value from the log.

### Checks

| Check | Category | Expected range | Observed |
|---|---|---|---|
| Hit damage | `Damage` | `avg × (1 − DamageSpread) − DamageAbsoluteTolerance` to `avg × (1 + DamageSpread) × (1 + UnexplainedHighMargin) + DamageAbsoluteTolerance`; `avg` is `landed.BaseDamage` (grievous overflow included) times `CritMultiplier` on a logged crit | `UnblockedAmount` |
| Partial tick damage | `Damage` | The hit damage range widened by `avg / TickFraction × PartialTickFractionTolerance` on each side | `UnblockedAmount` |
| Damage with overkill | `Damage` | 0 to the hit damage upper bound | `UnblockedAmount` |
| Crit on a hit that cannot crit | `Hits` | No crit | Crit |
| Forced crit | `Hits` | `HitRequest.ForcedCrit` | The log hit's crit |
| Engine hit with no log hit | `Hits` | The hit damage range | 0 |
| Log hit with no engine hit | `Hits` | 0 | `UnblockedAmount` |
| Cast time | `Casts` | `GetAdjustedCastTime` at the start ± `CastTimeToleranceSeconds` | `complete_cast` time − `begin_cast` time |
| Instant or cast | `Casts` | The engine's cast time (0 or above) | 0 for a `cast`, the logged duration for a `begin_cast` |
| Global cooldown | `GlobalCooldown` | `GlobalCooldownEndTime − GlobalCooldownToleranceSeconds` or later | The cast's `Time` |
| Cooldown | `Cooldown` | Now plus `Hero.GetCooldownRemains` or later | The cast's `Time` |
| Other `Ability.CanCast` refusal (resources, requirements) | `Casts` | `CanCast` true, with its `reason` | The cast; no numeric range |
| Still casting | `Casts` | The engine cast's `EndTime` or later | The next cast's `Time` |
| Periodic tick time | `Ticks` | The engine's predicted tick time ± `TickTimeToleranceSeconds` | The log tick's time |
| Periodic tick count | `Ticks` | The engine's full ticks plus its partial tick | The log ticks reserved by the effect |
| Pre-cast resource | `Resources` | The engine amount ± the `LogResources` tolerance | The log reading |
| Pre-cast aura | `Auras` | The engine stacks | The log stacks within `AuraToleranceSeconds` |
| Proc with zero chance | `Procs` | No proc | Proc evidence at the roll |

`IssueCategory` gains `GlobalCooldown`, `Cooldown` and `Ticks`. `ReplayOptions` gains `TickTimeToleranceSeconds`,
default `PeriodicEffect.ServerFrameSeconds`.

### Disagreement record

```csharp
public sealed record ExpectedRange(double Low, double High, string Unit);
```

`ReplayIssue` gains:

| Member | Content |
|---|---|
| `Expected` | `ExpectedRange`; `Low` or `High` is infinite for a one-sided range; null for a check with no numeric range |
| `Observed` | The log value |
| `Deviation` | `Observed − Low` below the range, `Observed − High` above it; null with no `Expected` |
| `RelativeDeviation` | `Deviation` over the nearer bound (null when that bound is 0 or infinite) |
| `State` | `EngineState` at the check |

`HitPair` gains `ExpectedLow`, `ExpectedHigh` and `State`. `HitPair.EngineAmount` is `avg`. `HitPair.Context` and
`HitContext` become `HitPair.State` and `EngineState`.

### Engine state

`EngineState` is built by `ReplayVerifier` at the check's time. Each value comes from the member named.

Hero:

| Field | Source |
|---|---|
| Primary attribute | `Hero.PrimaryAttribute` |
| Crit chance, and against the target | `Hero.CritChance`, `Hero.CritChanceAgainst` |
| Crit multiplier | `Hero.CritMultiplier` |
| Haste | `Hero.GetHaste` |
| Expertise | `Hero.Expertise` |
| Spirit | `Hero.SpiritPercent` |
| Damage scale | `Hero.DamageScale` |
| Resources | Each `ResourceDefs` entry, current and max |
| Global cooldown end | `Hero.GlobalCooldownEndTime` |
| Current cast and channel | `Hero.CurrentCast`, `Hero.CurrentChannel` |
| Auras | Each active aura: name, log ID, stacks, time left, and each modifier as `{aura} x{stacks}: {type} {op} {value}` |
| Active enemies | `SimulationContext.ActiveEnemies` count |

Target (for checks with a target):

| Field | Source |
|---|---|
| Unit and name | `EnemyTarget.Id`, `InstanceId`, `Name` |
| Health fraction | `EnemyTarget.HealthFraction` |
| Auras | Each active aura on `target.Auras`, with stacks, time left and modifiers in the hero aura format |
| Incoming damage multiplier | `target.Auras.Fold(StatType.IncomingDamageMultiplier, 1.0, now)` |

Hit (for hit and tick checks):

| Field | Source |
|---|---|
| Ability and damage ID | `HitRequest.AbilityName`, `AbilityId` |
| Base damage at landing | `HitRequest.BaseDamage` |
| Hit damage multiplier | `PendingHit.DamageMultiplier` |
| Ability damage multiplier | `Hero.AbilityDamageMultiplier` |
| Target damage multiplier | `Hero.DamageMultiplierAgainst` |
| Crit chance, cast and hit bonuses | `HitRequest.CritChance`, `PendingHit.CastCritChance`, `PendingHit.ExtraCritChance` |
| Crit multiplier | `HitRequest.CritMultiplier` |
| Tick fraction | `HitRequest.TickFraction` |
| Snapshot | `PendingHit.Snapshot` |
| Release and landing times | `HitPair.EngineTime`, `HitPair.LandedTime` |

Cast (for cast, global cooldown, cooldown and resource checks):

| Field | Source |
|---|---|
| Cast time | `Ability.GetAdjustedCastTime` |
| Global cooldown and its haste scaling | `Ability.GlobalCooldownSeconds ?? Hero.GlobalCooldownSeconds`, `GlobalCooldownScalesWithHaste` |
| Cooldown left and charges | `Hero.CooldownOf(ability)` |
| Cooldown rate | `Hero.CooldownRate` |
| Refusal | `Ability.CanCast` reason |

`ReplayIssue.ToString` appends the expected range, observed value and deviation. `ReplayReportWriter.Summary` therefore
lists them for each halt issue.

## Results and review page

- `ReplayResult.EngineTotalDamage` is the sum of `HitPair.EngineAmount`. `ReplayReportWriter.DamageTotals` returns it
  with the log total. `SimulationContext.TotalDamage` equals the log total.
- The review page has a **Disagreements** section per `IssueCategory`: time, step, subject, expected range, observed,
  deviation and relative deviation, with each row's `EngineState` expandable below it.
- The hit table shows engine `avg`, expected low and high, log amount and deviation.
- `HitsCsv` columns: `time, ability_id, ability, target, engine_amount, expected_low, expected_high, engine_crit,
  crit_chance, log_amount, log_crit, log_time, deviation, tick_fraction, player_auras, target_auras`.
- `ReplayOptions.StopOnFirstFailure` halts at the first step with a `ReplayIssue` from the verifier.

## Tests

- `ReplayVerifierTests`: one test per check row, each with a value inside the range, one below and one above (or the
  one side for a one-sided range), asserting `Expected`, `Observed`, `Deviation` and `RelativeDeviation`.
- `ExactReplayTests`, over a small replay script built in the test with a `Fixtures` character:
  - `ctx.Outcomes` is the `LogOutcomeSource`, and every `HitOutcome` from `ResolveHit` has `Spread` 1.0.
  - `SimulationContext.TotalDamage` equals the sum of `UnblockedAmount` over the script's damage observations.
  - Every log hit is dealt once: matched or through `DealLoggedHit`.
  - An engine hit with no log hit has a `HitPair` with its expected range and a `Hits` issue.
  - Each periodic tick with a damage log ID fires at its reserved log hit's time; a clipped effect releases its
    reservation; a missing log tick leaves the next tick's pairing in place.
- `CombatLogReplayTests` and the `ArdeosReplayTests`, `GundeReplayTests`, `MaraReplayTests` and `TariqReplayTests`
  replay every fight in `data/fights` and assert `ReplayResult.MatchedLogHits > 0`, every log hit dealt once, and
  `Expected` and `Observed` on every `ReplayIssue` with a numeric check.
- `MonteCarloSeedTests` assert the totals for each seed against golden values captured at commit 8fecd72.
