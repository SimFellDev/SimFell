---
name: replay-fix
description: Use when asked to fix what a hero's replay findings show, to work the findings JSON, or when invoked as /replay-fix <Hero> [count]. Measures the hero's fights on the current branch, picks findings from data/accuracy/<Hero>/<label>.findings.json in the order of work, has one replay-finding-fixer run work each, and keeps a change when the findings comparison shows a gain on its own finding.
argument-hint: <Hero> [count]
---

# Replay fix: a hero's findings, one at a time

The arguments are a hero and how many findings to work (default 5). This session leads: it measures, picks each finding,
starts the fixer, and decides from the comparison. `docs/REPLAY_ACCURACY.md` has the targets, the rules of evidence, the
document's fields and the place of each change; read it first.

The work stays on the current branch. Commits stay local: the skill pushes nothing and opens no pull request.

## The one hero

The work covers `<Hero>` alone: `<Hero>`'s players in the fights, `<Hero>`'s classes and `NOTES.md`, and the game data of
`<Hero>`'s abilities, effects, items and the shared systems they use. Every prompt and message states this. `dotnet test
-c Release` passing is the check of a change in shared code, read as pass or fail. A run that names, measures or reads
another hero is stopped with TaskStop, and a new run takes over with this section in its prompt.

## 1. Check

1. The heroes are the folders of `src/FellowshipSim.Core/Heroes`. An argument with no folder stops the skill with that list.
2. `data/fights` has at least two of the hero's fights of the current patch. With fewer, the skill stops and gives the user
   the `tools/fetch-fights.cs` command from `CLAUDE.md`: a mechanic needs two fights.
3. The working tree has no uncommitted change to `src/`. With one, the skill stops and asks the user what to do with it.
4. The game-data worktree is `G:/source/Fellowship_Data_Extractor.<branch>`, for the current branch name. With none:

   ```
   wt -C G:/source/Fellowship_Data_Extractor switch --create <branch> --no-cd
   ```

## 2. Measure

Labels are `fix-00`, `fix-01` and on, continuing from the highest in `data/accuracy/<Hero>`. Every measure rewrites each
fight's files in `data/reviews`. The starting measure:

```
ACCURACY_HERO=<Hero> ACCURACY_LABEL=fix-<NN> ACCURACY_FREEDICE_ITERATIONS=0 dotnet test tests/FellowshipSim.Tests -c Release --filter HeroAccuracyTests
```

The last accepted label is `<accepted>`. Every later measure adds `ACCURACY_COMPARE=<accepted>`. A comparison with a
name in `fightsAdded` or `fightsRemoved` is over two fight sets: this session measures the accepted code on the new set
under a new label, which becomes `<accepted>`, before it judges anything.

## 3. Pick

Read the document's `totals`, `unmodeled` and each finding's `rank`, `key`, `group`, `name`, `impact`, `issues` and
`fightsOff`:

```powershell
$d = Get-Content data/accuracy/<Hero>/<accepted>.findings.json -Raw | ConvertFrom-Json
$d.totals; $d.unmodeled
$d.findings | Where-Object group -ne WithinTargets | ForEach-Object { "$($_.rank) $($_.key) $($_.group) $($_.name) impact=$($_.impact) issues=$($_.issues) fightsOff=$($_.fightsOff)" }
```

Each pick is the first of these:

1. A name in `unmodeled.abilities` or `unmodeled.customizations`. Its subject is the name.
2. The finding with the lowest `rank`, its `fightsOff` at 2 or more, with no earlier result this run.

A finding with `fightsOff` of 1 is one fight's pattern. It is reported to the user and not worked.

## 4. Fix

The fixer's input is one slice:

```powershell
& .claude/skills/replay-fix/finding-slice.ps1 -Hero <Hero> -Label <accepted> -Key <key> -OutFile <scratchpad>/<NN>.finding.json
```

Two teammates, each started with the Agent tool and a `name`, in the background:

| Name | Agent type | Works in | Prompt names |
| --- | --- | --- | --- |
| `fixer-<NN>` | `replay-finding-fixer` | this worktree | `<Hero>` as the one hero, the slice's path, `<accepted>`, the next label, `game-data-authority` |
| `game-data-authority` | `game-data-authority` | the extractor worktree | `<Hero>` as the one hero, the branch, the extractor worktree path, the current fixer |

`game-data-authority` stays for the whole run. Each new fixer's name goes to it by SendMessage. Each fixer stops with its
change uncommitted.

## 5. Decide

The fixer's report gives an outcome. For `changed`, this session reads the new document's `comparison` itself:

```powershell
(Get-Content data/accuracy/<Hero>/<next label>.findings.json -Raw | ConvertFrom-Json).comparison | ConvertTo-Json -Depth 5
```

A change is kept when all of these hold:

- The finding's key is in `resolved`, or in `changed` with a lower `impactAfter` (a damage ID) or lower `issuesAfter` (a
  resource, aura, ability or proc). An unmodeled name is in `unmodeledResolved`.
- `impactAfter` of the whole `comparison` is at most `impactBefore`.
- Each key in `appeared`, and each key in `changed` with a higher `impactAfter`, has an explanation in the fixer's report.
- Every constant in the diff names its source, and that source is the authority's answer or a log pattern over two fights.
  A multiplier equal to the finding's ratio with no such source fails this.
- `dotnet test -c Release` passes.

A kept change is one commit, in the repository's style: `<Hero>: <what the game does>` or `Replay: <what the replay does>`,
with no attribution lines. Its label becomes `<accepted>`.

A change that fails goes back to the same fixer once, by SendMessage, with the failed condition. A second failure ends the
finding: the fixer undoes its edits with Edit, and the finding goes into the hero's `NOTES.md` under "Not yet explained".

After any undo, this session measures the accepted code again with `ACCURACY_LABEL=<accepted>`: the fight files then match
the label, and the slice refuses them otherwise. An exact replay gives the same document for the same code.

For `not-yet-explained`, the session checks the `NOTES.md` line has the numbers, the pattern and every source checked,
and commits it.

## 6. End

The run ends at `count` findings, or at no finding to pick. This session stops `game-data-authority` with TaskStop. The
report to the user has:

- each finding: its key, name, outcome, commit, and `impact` or `issues` before and after;
- the first and last label's `totals.impact`, `totals.issues` and hits matched;
- each one-fight pattern;
- the targets of `docs/REPLAY_ACCURACY.md` still unmet. The measure with free dice is `ACCURACY_FREEDICE_ITERATIONS=100`
  before a pull request.

## Reading a finding

The fixer works from the first row that fits. Each row names the numbers that decide the cause.

| Group | Numbers that decide | Cause | First read |
| --- | --- | --- | --- |
| `UnmodeledContent` | `damage.missing` equals `damage.logHits` | the engine has no hit with this ID | the ID in `fellowship-codex`, then `docs/ADDING_A_HERO.md` |
| `Calibration` | `normal.mean` and `crit.mean` agree, off 1 | a missing or wrong multiplier | `sampleHits[].state.hit` and the auras against the ability's game data |
| `Calibration` | `crit.mean` off, `normal.mean` on 1 | crit power | `state.hero.critMultiplier` and each `CritMultiplier` aura |
| `Calibration` | a `splits` row with `ratioWithout` near 1 | that condition's multiplier | the condition's aura or enemy |
| `Calibration` | `gates.scattered`, means on 1 | two kinds of hit share the ID | `hits.csv` for the ID, split by target count and auras |
| `HitCounts` | `engineOnly` and `missing` in similar counts, `releaseFits` high | timing or pairing: the hits exist and pair wrongly | `lagMedianSeconds`, `lag90Seconds`, then `byFight` for fights in `ReplayPairing` |
| `HitCounts` | `engineOnly` alone, `engineOnlyGroups` well under `engineOnly` | a target cap or area | `activeEnemies` in `state.hero`, the ability's cap in game data |
| `HitCounts` | `engineOnly` alone, groups near hits | a proc chance, internal cooldown or tick period | the proc's chance and cooldown in game data |
| `HitCounts` | `missing` alone | a proc or chain the engine lacks | the raw events around `examples` of `missing engine hit` |
| `ReplayPairing` | `gates.mispaired`, `releases.mean` on 1 | the replay pairs hits of one release to the wrong log hits | `LogOutcomeSource`; the game's damage is right |
| `Ticks` | `tickOffsets.lateShare` near 0 or 1 | the tick period or its haste | the effect's period and haste flag in game data |
| `Ticks` | `lateShare` near 0.5 | a tolerance question: read the frames before any change | `ReplayOptions.TickTimeToleranceSeconds` and its evidence |
| `Resources` | `cancelling` near `issues` | one event logged on the other side of a check; `LogResources` | the raw events at two `examples` |
| `Resources` | `net` near `gross` | a cost or gain of the wrong size or missing | `afterAbilities`, then that ability's cost in game data |
| `Auras` | `net` and `afterAbilities` | a duration, stack count or trigger | the `examples` with `state`, then the aura's game data |
| `CastsAndCooldowns` | `issueKinds` | a cooldown, charge or global cooldown model | `state.cast`: `refusal`, `cooldownLeft`, `charges` |
| `Procs` | `proc.procs` against `proc.expectedProcs` | a chance, or the replay's evidence for a roll (`ProcEvidence`) | the proc's chance in game data |

`byFight` with one fight's `group` differing from the pooled `group` is itself a fact to report. `totals.issues` counts
every check off its range, so a cause usually moves several findings: `appeared` and `changed` show them.
