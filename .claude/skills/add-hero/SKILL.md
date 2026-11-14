---
name: add-hero
description: Use when asked to add a hero to the engine, or when invoked as /add-hero <Hero> <Role>. Sets up a worktree and the hero's current-patch fights, then has an implementer teammate build the hero in Rime's patterns and an independent reviewer teammate adversarially review it against the codex and the log replay, through to a pull request.
argument-hint: <Hero> <Role>
---

# Add a hero

The arguments are a hero and its role: `DPS`, `Tank` or `Healer`. This session leads. It sets up the work, starts two
teammates, routes between them and answers for the pull request. It reads code only to judge a report; the teammates
do the work.

## 1. Check the arguments

- The hero is one of the `heroes` that the codex tool `mcp__fellowship-codex__list_types` returns, and has no folder in
  `src/FellowshipSim.Core/Heroes`. Any other hero stops the skill with both lists.
- The role is `DPS`, `Tank` or `Healer`. Any other role stops the skill with that list.
- `<hero>` below is the hero's name in lower case.

## 2. Worktree

`EnterWorktree({name: "<hero>"})`. The session and its teammates work in this worktree from here on. A `<hero>` branch
that already exists, local or remote, stops the skill with its last commit.

`data/` is gitignored, so it has no files in a new worktree. Make a junction to the main checkout's data folder, and
remove it with `cmd /c rmdir data` at the end. Never delete through it.

```
cmd /c mklink /J data G:\source\SimFell\data
```

`codex-export/` is gitignored too. A build compiles the committed game data and needs none; the hero's new `partial`
classes get their generated halves from `tools/regenerate-codex.sh`, which reads it. Download it in the worktree:

```
tools/download-codex-export.sh
```

## 3. Fights

```
dotnet run tools/fetch-fights.cs -- --hero <Hero> --count 15
```

A fight is the hero's when `fight.friendlyPlayers` has a player whose `master_data.actors[].subType` is the hero. Fewer
than six of the hero's fights of the current patch stops the skill with the tool's last lines. The tool needs the
FellowshipLogs credentials, `FELLOWSHIPLOGS_CLIENT_ID` and `FELLOWSHIPLOGS_CLIENT_SECRET`, from the environment or the `simfell` user secrets; with none set, the session asks the user for them.

## 4. Pattern hero

The pattern hero is the hero with the same role that has the most fights in `data/fights`, and Rime when no hero of
the role has a folder yet. Each teammate's prompt names it.

## 5. What the replay measures for the role

| Role | Replay compares |
| --- | --- |
| DPS | the hero's damage done, hit by hit |
| Tank | the hero's damage done, and the damage the hero takes after its mitigation |
| Healer | the hero's damage done, and the hero's healing and absorbs, hit by hit |

A comparison the replay has no code for yet is the implementer's first change, in `src/FellowshipSim.Engine/Replay`,
committed alone and before the hero's own commits. Every existing hero's replay test still passes after it.

## 6. Teammates

Two teammates, each started with the Agent tool and a `name`, in the background:

| Name | Agent type | Prompt |
| --- | --- | --- |
| `review-<hero>` | `general-purpose` | Reviewer brief, stage 1 |
| `<hero>` | `general-purpose` | Implementer brief |

Start both in one message. Each prompt names the hero, the role, the pattern hero, the worktree path, the branch and the
other teammate's name, followed by its brief from this file.

1. The reviewer ends stage 1 with its codex list saved. The implementer reports its commits and replay numbers.
2. The session sends the reviewer the implementer's commit range and nothing else from the implementer's report. The
   reviewer judges the code and the data, never the implementer's summary.
3. Each confirmed defect from the reviewer goes to the implementer. The fix commits go back to the reviewer for
   verification on a fresh `git archive <hero>` copy. This repeats until the reviewer confirms no defects.
4. A constant in any report with no source goes back to the implementer before anything else.

### Implementer brief

- Read `CLAUDE.md`, `docs/ADDING_A_HERO.md`, `docs/REPLAY_ACCURACY.md`, the pattern hero's `Heroes/<Pattern>/**`, its
  smoke tests and replay tests, and its entries in `src/FellowshipSim.Engine/Replay/{LogResources,ProcEvidence,ClassTuningHistory}.cs`.
  Read `C:\Users\Sean\.claude\CLAUDE.md` and `C:\Users\Sean\.claude\output-grammer.md`; they apply to every comment,
  `NOTES.md` line and commit message.
- Build the hero in `src/FellowshipSim.Core/Heroes/<Hero>/`, in the pattern hero's style: abilities, talents,
  resources, the hero's legendaries, weapons and traits, `ActionLists/Default.simc`, a `NOTES.md` of the mechanics the
  logs showed, a default profile in `data/profiles/<Hero>/`, smoke tests for every talent and legendary, a replay test
  of the hero's fights, and the hero's entry in `AplLanguageTests`. `CLAUDE.md` names the hero.
- Shared items are reused, never copied: weapons such as Fated Strike and Sahril's Wrath, relic passives, affix
  debuffs, death and revive, `KeepsOnDeath`, hit types and blocked amounts.
- Game data comes from the codex: ToolSearch
  `select:mcp__fellowship-codex__find_entity,mcp__fellowship-codex__get_entity,mcp__fellowship-codex__list_types`.
  `find_entity` matches name prefixes only, so search ability and talent names; `get_entity` takes a `hero` argument.
  Each constant's comment names its source. `UNVERIFIED` marks a value no log has confirmed.
- A new `partial` hero, ability or talent class has no generated half until `tools/regenerate-codex.sh` runs. Run it
  after adding or renaming one, and commit `src/FellowshipSim.Core/Generated/Codex/` with the class
  (`docs/ADDING_A_HERO.md`, "Regenerating the game data").
- Validate with exact replay over every fight of the hero, and free dice where a proc or spread is in question:
  `ACCURACY_HERO=<Hero> ACCURACY_LABEL=<label> dotnet test tests/FellowshipSim.Tests -c Release --filter HeroAccuracyTests`.
- Commit in steps, each `<Area>: <summary>` (`<Hero>: abilities and talents`, `Replay: <Hero>'s <resource>`), with
  explicit pathspecs. A shared-file change is small, additive and its own commit.
- Before the final report, run the full suite, `dotnet test -c Release`, with every test passing.
- Final report, under 300 words: commits, files outside `Heroes/<Hero>` and the hero's tests, replay numbers per fight
  (hits matched and damage of the log, plus the role's other comparison), what is `UNVERIFIED`, known gaps.

### Reviewer brief

- Stage 1: read the same files as the implementer, then build an independent list of the hero's abilities, talents,
  legendaries and their coefficients from the codex. Save it to `.artifacts/review-<hero>-data.md` in the worktree and end the turn
  with one line.
- Stage 2, on the session's message: review the commit range. Check every constant against the codex list, every hook
  against the pattern hero and `ADDING_A_HERO.md`, every comment against the output grammar, and that shared-file
  changes keep every other hero's tests passing.
- Re-run the validation on a fresh `git archive <hero>` copy with its own data junction, and run the full suite there.
  Remove the junction with `cmd /c rmdir` afterwards.
- Read-only: no edits, commits or pushes.
- Report, under 300 words: confirmed defects only, each with `file:line`, the evidence (codex value against code value,
  or replay numbers) and the failure. Then the measured replay numbers.

## 7. Pull request

1. The implementer rebases the branch onto `upstream/main` and re-runs the full suite.
2. The session removes the data junction, pushes the branch to `origin` and opens a pull request on `SimFellDev/SimFell`
   against `main`. The body has what the hero has, the shared engine and replay changes, the full-suite count, a table
   of the replay numbers per fight, and the `UNVERIFIED` values and known gaps.
3. The session reports the pull request link and the replay numbers to the user, with `/replay-accuracy <Hero>` as the
   next stage.

## Rules

- Commit messages and pull request bodies have no Co-Authored-By or other attribution lines. Every teammate prompt says
  so.
- `git stash` is never run.
- Builds use `--artifacts-path .artifacts/<name>`, one name per teammate, with `.artifacts/` in `.git/info/exclude`.
- Sims and benchmarks use at most 8 cores.
- The worktree is removed with `wt remove <hero> -D` once its pull request has merged, and the merged remote branch is
  deleted.
