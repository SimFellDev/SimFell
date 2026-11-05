# Replay accuracy

The engine reproduces each hero's damage in the combat logs of the patch it models (`GamePatch.Current`). This page
states the targets, the measure and the rules of evidence. Four stages make one hero more accurate, each in its own
context:

| Stage | Run by | Output |
| --- | --- | --- |
| 1. Spec | `/replay-accuracy <Hero>`: the worktrees, the fights, the baseline, then the `accuracy-spec-writer` agent | `docs/accuracy/<branch>.md` |
| 2. Implementation | the same session: teammates `simfell-engineer` (SimFell worktree) and `game-data-authority` (extractor worktree) | commits and a pull request |
| 3. Adversarial review | `/replay-accuracy-review <branch>` in a new session | `docs/accuracy/<branch>.review.md` |
| 4. Spec audit | `/replay-accuracy-audit <branch>` in a new session: the `spec-conformance-auditor` agent | `docs/accuracy/<branch>.audit.md` |

`<branch>` is `<hero>-accuracy-v<N>`, the name of both worktrees.

`/replay-fix <findings.json>` works the findings of one downloaded review on the current branch: one
`replay-finding-fixer` run per finding, each change kept when the measure shows a gain on its own finding.

## The measure

`HeroAccuracyTests` measures one hero over every fight of the current patch in `data/fights`:

```
ACCURACY_HERO=Rime ACCURACY_LABEL=baseline dotnet test tests/FellowshipSim.Tests -c Release --filter HeroAccuracyTests
```

Each player of the hero gets an exact replay (the log's dice) and a free-dice run (the engine's dice, 100 runs by
default). The result is in `data/accuracy/<Hero>/<label>.txt` and `.json`. A label other than `baseline` prints each
headline number next to the baseline's.

`tools/fetch-fights.cs` downloads the fights:

```
dotnet run tools/fetch-fights.cs -- --hero Rime --count 15
```

It reads the hero's ranking lists in the latest zone from rank 1 down, one encounter after another. A run counts when it
started on or after the patch's release day, its report has the patch's log format, and its fight has a player of the
hero. Its last lines give the fights found against the count asked for, and the candidates skipped by reason.

## The findings document

A review's page has a **Findings JSON** button. The file has the review's findings for an agent, from
`ReplayFindingsDocument`, and it is the input of `/replay-fix`.

| Field | Contents |
| --- | --- |
| `schema` | the format's version (`ReplayFindingsDocument.Schema`) |
| `hero`, `patch` | the hero and the patch of the engine that wrote the document |
| `thresholds` | the tests' values, from `ReplayFindings` and `ReplayOptions` |
| `totals` | the fight's damage, hits, steps, forced casts and issues, findings per group, and `impact`: the sum over findings off target |
| `unmodeled` | `abilities`, `customizations`, `externalAuras` and `untrackedLogAuras`: step 1 of the order of work |
| `fight` | `review`, the fight, the player, and `files`: repository paths of each file of the fight |
| `findings[]` | one per `IssueEntity`, in the order of work, each group by `impact` |

A finding has:

| Field | Contents |
| --- | --- |
| `key` | `IssueEntity.KeyText`, the same in every review and every run: `DamageId:1001242`, `Resource:Mana`, `Aura:1001395`, `Ability:<name>`, `Proc:<key>` |
| `rank`, `group`, `kind`, `id`, `name` | the place in the order of work and the entity. `id` is `ability_id` in `hits.csv` |
| `impact` | the damage at stake as a fraction of the log's damage; absent for a resource, aura, ability or proc |
| `issues`, `issueKinds` | the count, and the count of each kind of issue |
| `damage` | a damage ID's numbers, and in `gates` the tests that placed it in its group |
| `drift` | a resource's or aura's `net` and `gross` log minus engine, `cancelling`, and `afterAbilities` |
| `proc` | `rolls`, `expectedProcs`, `procs` |
| `splits` | conditions dividing the ratio: the condition, then hits and mean ratio on each side |
| `examples` | the first, median and worst issue of each kind, with the step's ability; each kind's first has `state`, the engine's state |
| `sampleHits` | a damage ID's median and worst normal and crit hits, first engine-only and first missing hit, each with `state` |

A finding in `WithinTargets` has its numbers alone.

## Targets

| Measure | Target |
| --- | --- |
| Exact replay damage / log, each fight | 99% to 101% |
| Free dice mean / log, all fights | 98% to 102% |
| Mean log percentile in free dice | 30% to 70% |
| Calibration (mean log/engine), normal and crit, each damage ID over 1% of the log's damage | 0.98 to 1.02 |
| Log hits matched | 98% or more |
| Engine-only hits, each damage ID over 1% of the log's damage | 2% of its log hits or fewer |

The free-dice percentile is the share of the engine's runs below the log's damage. A model with the game's mechanics
puts the log near the middle of its runs. A percentile near 0% or 100% means the engine is low or high on average, or
its spread is too narrow.

## The order of work

1. **Unmodeled content.** Every name in `unmodeled abilities` and `unmodeled customizations` is damage the engine has
   no model of. Each one is built from its game data first.
2. **Calibration.** A damage ID whose normal and crit ratios agree and sit off 1.000 has a missing or wrong multiplier:
   a coefficient, a talent, a buff, a party buff (`PartyBuffs`), class tuning (`ClassTuningHistory`) or target falloff.
   A crit ratio off on its own is crit power.
3. **Hit counts.** Missing and engine-only hits of one damage ID are a timing, proc or targeting model: tick period,
   haste on the period, partial ticks, travel time, proc chance or internal cooldown, target cap.
4. **Resources and auras.** A resource or aura drifting from the log changes later casts and procs. Its first
   occurrence in a fight is the place to read.

Each step ranks damage IDs by `log damage x |1 - ratio|`, largest first.

## Evidence

Combat logs are inexact:

- A timestamp is the server frame's, about 33ms, and events of one frame are logged up to a few milliseconds apart.
- Events of one frame are in no fixed order: a cast, its hit, its aura and its resource change appear in any order.
- An event is missing from a log at times: an aura removal at a fight's end or on death, a resource update, a hit on an
  enemy the logger never saw arrive.
- Resource values are snapshots and round in the log.
- A hit's amount is after the enemy's damage taken modifiers. An enemy debuff from another player changes it.

`ReplayOptions` holds each tolerance the replay applies, with the log evidence for it in its comment. A tolerance
changes only with log evidence for the new value.

A mechanic is established by:

1. **Game data first.** The datamine export (`entities.jsonl` in `Fellowship_Data_Extractor/output/export-<build>/`),
   then the cooked assets and Blueprint bytecode the extractor dumps, then the shipping binary by static disassembly.
   The game data gives the mechanic. A log confirms it.
2. **A pattern in the logs.** A mechanic read from logs holds in at least two fights and over every occurrence in them.
   One event is an example, never a proof.
3. **A source for every value.** Every constant, multiplier, period, chance and cap in the engine names its source:
   a game data record, an asset property, a Blueprint function, a binary address, or a log pattern with its fights.
   A difference with no such source stays in the measure as a difference. It has a line in the hero's `NOTES.md` under
   "Not yet explained", with its numbers and every source checked. The engine has no constant chosen to make a number
   match: Abilities whose log damage is 1.4 times the engine's keep their engine damage until a source gives the
   1.4.

## Where a change belongs

| Finding | Place |
| --- | --- |
| A game value or mechanic | the hero's classes in `FellowshipSim.Core/Heroes/<Hero>`, with the value's source in its comment |
| How the log shows a resource, proc, party buff or past tuning | `FellowshipSim.Engine/Replay` (`LogResources`, `ProcEvidence`, `PartyBuffs`, `ClassTuningHistory`) |
| How the replay pairs log events with engine events, and compares them | `FellowshipSim.Engine/Replay` (`ReplayDriver`, `LogOutcomeSource`, `ReplayVerifier`, `ReplayOptions`) |
| What the logs showed, with the fights | `Heroes/<Hero>/NOTES.md` |
| A value the datamine export lacks | a reader in `Fellowship_Data_Extractor`, and the export re-run |

Core has no knowledge of logs. A change in `FellowshipSim.Engine/Replay` applies to every hero, so `dotnet test`
replays every downloaded fight before it is committed.
