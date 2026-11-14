---
name: apl-from-logs
description: Use when asked to build or improve a hero's action list from a FellowshipLogs report, or when invoked as /apl-from-logs <report link> [<player>]. Reads how the logged player plays the hero, writes a short action list a human can play, and sims it with the player's character on the logged run's dungeon until its casts match the log's, for the user's review.
argument-hint: <report link with ?fight=N> [<player id or name>] [--model <opus|sonnet|fable|haiku>] [--effort <low|medium|high|xhigh|max>]
---

# Action list from a FellowshipLogs report

The argument is a FellowshipLogs link with its fight (`?fight=N`), and the player as `&source=N` or as a second argument.
The player's hero is `<Hero>`. The result is one action list for the user's review, with the sim's numbers against the log's.

## Model and effort

Steps 1 to 5 run in one agent: the Agent tool, `subagent_type: general-purpose`, `name: apl-from-logs`, in the background,
with no worktree. Its `model` is `opus` and its `effort` is `high`, unless the arguments have `--model` or `--effort`; an
argument's value replaces the default. A user's words in the request ("use Sonnet", "at medium effort") are the same as the
argument.

Its prompt has the link, the player, `<out>`, this file's path with steps 1 to 5 as its work, and these limits: the current
branch, no change to engine or repository code, no commit, and the report of step 5 as its final message. This session gives
the user the report. The user's changes to the list go to the agent by SendMessage while it runs, and the agent runs once
more for each. Step 6 is this session's.

The action list and every scratch file are in `<out>`: `<scratchpad>/apl-from-logs/<report>_fight<N>_p<player>/`.
Nothing is committed until the user asks (step 6).

## The list

These are the user's requirements:

- A human can play it: a short priority list of 10 to 25 actions, with no frame-perfect timing.
- It reads enemy health, target count and pull type.
- It follows the logged player: the abilities they press, how often, on how many enemies, at what resources, on bosses and
  on trash.

Each action has a `#` comment line above it with the reason and the log numbers behind it. Every name is in the hero's
vocabulary, and the engine's check of the list reports no error. `src/FellowshipSim.Core/Heroes/Mara/ActionLists/Mara (dungeon).simc`
is a finished list.

## 1. Prepare

```
dotnet run .claude/skills/apl-from-logs/prepare.cs -- "<link>" [<player>] <out>
```

It writes `fight.json`, `dungeon-fight.json`, `dungeon.simc` (the run's pulls, as the app's dungeon import reads them) and
`character.simc` (the player's gear, talents and stats), and downloads the raw fight into `data/fights` once. Each run spends
FellowshipLogs points, so it runs once per fight. A report from before `GamePatch.Current` is named in its output; the
session tells the user and carries on.

```
dotnet run .claude/skills/apl-from-logs/log-tables.cs -- <out>
```

It writes `log-tables.txt`, `log-casts.txt` (every cast in order) and `log.json` (for step 4).

## 2. Read the play

Read `CLAUDE.md`, `docs/ADDING_A_HERO.md`, `src/FellowshipSim.Core/Heroes/<Hero>` (abilities, talents, `NOTES.md`) and
`ActionLists/Default.simc`, then `log-tables.txt`:

| Table | What it decides |
| --- | --- |
| Casts | the abilities in the list and their order of priority; the mean resources at each cast |
| Casts a minute by enemies alive | `active_enemies` thresholds; boss and trash differences |
| Each ability's casts | resource thresholds (`combo_points>=5`), target health thresholds, enemies hit, and the next cast on the global cooldown |
| Buffs at cast against uptime | the buffs the player waits for or spends: a buff at 99% of an ability's casts with 70% uptime is a condition of that ability |
| Debuffs on the target | `dot.<name>.remains` and `debuff.<name>.up` conditions |
| Damage by damage ID | the log side of step 4 |

The action-list grammar is in `src/FellowshipSim.Core/APL`. `Expressions/Expressions.cs` has the named values: `active_enemies`,
`target.health.pct`, `target.time_to_die`, `target.is_boss`, and `dungeon_event.boss.exists`, `dungeon_event.adds.in` and the
other `dungeon_event` values of a dungeon fight. The buff table writes each buff's action-list name, in `[brackets]` when the
hero's vocabulary does not have it. For each aura the list reads, the session checks the hero's code: the aura behind the name
is the one with the log's buff ID. A name of a stacks buff and its active buff can differ by one word (`deadly_scheme` and
`deadly_scheme_active`).

## 3. Write

The first version starts from `Default.simc` and goes to `<out>/v1.simc`; each later version is the next number, with a
letter for variants of one change (`v3a`, `v3b`). An ability the sim does not cast (an interrupt, a defensive, movement, a
mount) has no action.

## 4. Measure

```
dotnet run .claude/skills/apl-from-logs/compare.cs -- <out> <out>/v<N>.simc [<iterations>=200] [<threads>=8]
```

Each run is in the background, one run at a time, with 8 threads at most. It checks the list first, and stops with each
problem when the check fails. It sims the logged character on the logged dungeon with no length variance, and prints mean
enemies alive and hero DPS, each damage ID's DPS, hits a minute, average hit and crit, a total for each damage name under
more than one ID, and each ability's casts a minute, sim against log. `<out>/runs/` has each version's whole profile
(`.simc`), result (`.json`) and table (`.txt`).

A version is better when its casts a minute and its conditions at cast are nearer the log's. These gaps come from outside
the list, and the report names each with its next step:

- More enemies alive in the sim than in the log: more hits a minute of every area ability. The dungeon import is the cause.
- A gap in the average hit or crit of a damage ID: the engine's model of `<Hero>`, the character or the party's buffs.
  `/replay-accuracy <Hero>` is the next step.
- A rate the player reached by wasting a resource, such as charges held at their maximum. The list does not copy it.

## 5. Report

The report to the user has:

- the list's path and its action count, and the check's result;
- the values the list reads for target count, pull type and health;
- hero DPS of the log, of `Default.simc` and of the list;
- the damage and casts-a-minute tables of the list's run;
- the player's play, with the log numbers behind each action;
- each gap after the last version, and its cause: the list, the dungeon import, the engine's model of `<Hero>`, or the
  character and party buffs.

The work stops at the report, for the user's review.

## 6. After the review

When the user asks for the list in the repository, it goes to
`src/FellowshipSim.Core/Heroes/<Hero>/ActionLists/<Name>.simc` under the name the user gives, and `dotnet test -c Release`
runs; `AplLanguageTests` binds and casts every list of every hero. A commit follows only when the user asks for one, in the
repository's style (`<Name>: an action list from <player>'s <dungeon> run`), with no attribution lines.
