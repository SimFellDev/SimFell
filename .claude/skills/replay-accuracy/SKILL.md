---
name: replay-accuracy
description: Use when asked to make the engine's log replay of one hero more accurate, or when invoked as /replay-accuracy <Hero>. Runs stages 1 and 2 of docs/REPLAY_ACCURACY.md - same-named SimFell and Fellowship_Data_Extractor worktrees, the hero's current-patch fights, the baseline, the spec, then a SimFell teammate and a game-data teammate working the spec to a pull request.
argument-hint: <Hero>
---

# Replay accuracy for one hero: spec and implementation

The argument is a hero. The session runs on Opus 5.5 at high effort. This session leads. It sets up the work, has the spec written, starts two teammates, and
answers for the pull request. `docs/REPLAY_ACCURACY.md` has the stages, the targets, the measure and the rules of
evidence; read it first.

## 1. Check the hero

The heroes are the folders of `src/FellowshipSim.Core/Heroes`. An argument with no folder stops the skill with that
list. `<hero>` below is the hero's name in lower case.

## 2. Worktrees

Both repositories get one worktree, with one name, `<branch>` = `<hero>-accuracy-v<N>`. `N` is one more than the
highest `N` of a `<hero>-accuracy-v*` branch in either repository, local or remote, and 1 when there is none:

```
git -C G:/source/SimFell branch -a --list "*<hero>-accuracy-v*"
git -C G:/source/Fellowship_Data_Extractor branch -a --list "*<hero>-accuracy-v*"
```

1. SimFell: `EnterWorktree({name: "<branch>"})`. The session and its teammates work in this worktree from here on.
2. Extractor, with the session staying in SimFell:

   ```
   wt -C G:/source/Fellowship_Data_Extractor switch --create <branch> --no-cd
   ```

   Its path is `G:/source/Fellowship_Data_Extractor.<branch>`.

## 3. Fights

`data/` has no files in a new worktree. The fights go into this worktree's `data/fights`:

```
dotnet run tools/fetch-fights.cs -- --hero <Hero> --count 15
```

The tool spreads the fights over the zone's encounters. Fights of the current patch in the main checkout's
`data/fights` are copied in as well; the measure picks out the hero's fights of the current patch, so the whole folder
can be copied.

Fewer than six usable fights is too few to tell a pattern from one fight's events: the session stops and reports the
tool's last lines to the user. The tool needs the FellowshipLogs credentials the app uses; with none set, the session
stops and asks the user for them. FellowshipLogs allows about 18,000 points an hour for one API client, shared by every
session, so a session downloads its fights once, here.

## 4. Baseline

```
dotnet build -c Release
ACCURACY_HERO=<Hero> ACCURACY_LABEL=baseline dotnet test tests/FellowshipSim.Tests -c Release --no-build --filter HeroAccuracyTests
```

`data/accuracy/<Hero>/baseline.txt` is the start. It is never written again in this worktree.

## 5. Spec

An `accuracy-spec-writer` agent writes `docs/accuracy/<branch>.md` from the baseline. Its prompt names the hero, the
branch, the worktree and the baseline file. The session copies `data/accuracy/<Hero>/baseline.txt` and `.json` to
`docs/accuracy/<branch>.baseline.txt` and `.json`, and commits them with the spec on the branch, alone in their commit,
before any change to the engine. The review and the audit read the baseline from there. The spec is the contract for stages 2 to 4; a change to it after this commit is its own commit
with the reason in the message.

## 6. Implementation

Two teammates, each started with the Agent tool and a `name`, in the background:

| Name | Agent type | Works in | Prompt names |
| --- | --- | --- | --- |
| `simfell-engineer` | `simfell-engineer` | this SimFell worktree | the hero, the branch, the spec, the extractor worktree path |
| `game-data-authority` | `game-data-authority` | the extractor worktree | the hero, the branch, the extractor worktree path, the teammate `simfell-engineer` |

Both teammates run on Opus 5.5, by their definitions. A question whose answer is in the shipping binary or in
Blueprint bytecode goes to a third run, started by the session when `game-data-authority` asks for it: agent type
`game-data-authority`, `name: "game-data-binary"`, `model: "fable"`, `effort: "high"`, in the extractor worktree, its
prompt naming the question, the rungs checked, and the teammate `simfell-engineer`. A `simfell-engineer` run that ends
with large items under "Not yet explained" is run again on those items with `model: "fable"`.

The teammates message each other with SendMessage. A message to a teammate whose turn has ended starts its next turn.
The session routes between them when a report comes back with a question for the other teammate, and resumes
`simfell-engineer` until its report gives one of two results: every spec item met, or each remaining item under "Not yet
explained" in the hero's `NOTES.md` with the sources checked.

The session reads every report it receives. A constant in a report with no source, or a source the report does not
name, goes back to `simfell-engineer` before anything else.

## 7. Pull request

1. `simfell-engineer` runs the measure with `ACCURACY_LABEL=final` and `ACCURACY_FREEDICE_ITERATIONS=100`.
2. The session rebases the branch onto `origin/main`, pushes it, and opens a pull request on `Seriousnes/SimFell`. Its
   body has the baseline and final headline numbers, each finding with its source, and the "Not yet explained" lines.
3. An extractor worktree with commits gets its own pull request on its repository, and the SimFell pull request links
   it. An extractor worktree with no commits is removed:
   `wt -C G:/source/Fellowship_Data_Extractor remove <branch>`.
4. The session reports the pull request links and the final numbers to the user, with the next two stages:
   `/replay-accuracy-review <branch>`, then `/replay-accuracy-audit <branch>`, each in a new session.

## Rules

- Commit messages and pull request bodies have no Co-Authored-By or other attribution lines.
- `git stash` is never run, in either worktree.
- The game is never launched, and no debugger is attached to it. The shipping binary is read by static disassembly
  alone.
- Each worktree is removed with `wt remove` once its pull request has merged.
