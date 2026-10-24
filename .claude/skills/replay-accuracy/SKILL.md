---
name: replay-accuracy
description: Use when asked to make the engine's log replay of one hero more accurate, or when invoked as /replay-accuracy <Hero>. Creates same-named SimFell and Fellowship_Data_Extractor worktrees, downloads the hero's fights of the current patch, measures the engine against them, and fixes the differences with game data as the source.
argument-hint: <Hero>
---

# Replay accuracy for one hero

The argument is a hero. `docs/REPLAY_ACCURACY.md` has the targets, the measure and the rules of evidence. Read it, then
`docs/ADDING_A_HERO.md`, `src/FellowshipSim.Core/Heroes/<Hero>/NOTES.md` and the hero's classes before the first change.

## 1. Check the hero

The heroes are the folders of `src/FellowshipSim.Core/Heroes`. An argument with no folder stops the skill with that
list. `<hero>` below is the hero's name in lower case.

## 2. Worktrees

Both repositories get one worktree, with one name: `<hero>-accuracy-v<N>`. `N` is one more than the highest `N` of a
`<hero>-accuracy-v*` branch in either repository, local or remote, and 1 when there is none:

```
git -C G:/source/SimFell branch -a --list "*<hero>-accuracy-v*"
git -C G:/source/Fellowship_Data_Extractor branch -a --list "*<hero>-accuracy-v*"
```

1. SimFell: `EnterWorktree({name: "<hero>-accuracy-v<N>"})`. The session works in this worktree from here on.
2. Extractor, with the session staying in SimFell:

   ```
   wt -C G:/source/Fellowship_Data_Extractor switch --create <hero>-accuracy-v<N> --no-cd
   ```

   Its path is `G:/source/Fellowship_Data_Extractor.<hero>-accuracy-v<N>`.

## 3. Fights

`data/` has no files in a new worktree. The fights go into this worktree's `data/fights`:

```
dotnet run tools/fetch-fights.cs -- --hero <Hero> --count 12
```

Fights of the current patch in the main checkout's `data/fights` are copied in as well. The measure picks out the hero's
fights of the current patch, so the whole folder can be copied.

Twelve fights cover the zone's dungeons and several builds. Fewer than six usable fights is too few to tell a pattern
from one fight's events: the session stops and reports the tool's last lines to the user. The tool needs the
FellowshipLogs credentials the app uses; with none set, the session stops and asks the user for them.

FellowshipLogs allows about 18,000 points an hour for one API client. Sessions for several heroes share it, so a session
downloads its fights once, at the start.

## 4. Baseline

```
dotnet build -c Release
ACCURACY_HERO=<Hero> ACCURACY_LABEL=baseline dotnet test tests/FellowshipSim.Tests -c Release --no-build --filter HeroAccuracyTests
```

`data/accuracy/<Hero>/baseline.txt` is the start. It is never written again in this worktree.

## 5. The loop

Each pass takes the largest difference by the order in `docs/REPLAY_ACCURACY.md`, and:

1. **Reads it.** The review pages give the detail: `dotnet test -c Release --no-build --filter CombatLogReplayTests`
   writes `data/reviews/<review>.html`, `.summary.txt` and `.hits.csv` for every fight. A `replay-discrepancy-analyst`
   agent reads one damage ID, aura or resource across the fights and returns its pattern with the fights and times.
   Several run at once for independent damage IDs.
2. **Finds the mechanic in the game data.** A `fellowship-datamine` agent works in the extractor worktree and returns a
   fact with its source: an export field, an asset path and property, a Blueprint function and offset, or a binary
   address. The `fellowship-codex` MCP tools read the main checkout's export, enough for a value lookup.
3. **Changes the model.** The table in `docs/REPLAY_ACCURACY.md` names the place. Each game value is a constant at the
   top of its class, with its source in the comment, as `CLAUDE.md` requires.
4. **Measures.** The measure runs with a new label, such as `pass-03`, and prints the baseline beside each number. A
   change with no gain on its own damage ID is reverted.
5. **Checks every hero.** `dotnet test -c Release` replays every downloaded fight. A change in
   `FellowshipSim.Engine/Replay` applies to every hero.
6. **Records.** The hero's `NOTES.md` has each mechanic the logs showed, with the fights. One commit per finding, on this
   worktree's branch, in the repository's style: `<Hero>: <what the game does>` or `Replay: <what the replay does>`.

The loop ends when every target in `docs/REPLAY_ACCURACY.md` is met, or when the remaining differences have no source in
the game data and no pattern in the logs. Each of those has a line in `NOTES.md` under "Not yet explained", with the
measure's numbers for it.

## 6. Finish

1. The measure runs with `ACCURACY_LABEL=final`.
2. The branch is rebased onto `origin/main` and pushed, and a pull request opens on `Seriousnes/SimFell`. Its body has
   the baseline and final headline numbers, each finding with its source, and the "Not yet explained" lines.
3. An extractor worktree with commits gets its own pull request on its repository, and the SimFell pull request links
   it. An extractor worktree with no commits is removed: `wt -C G:/source/Fellowship_Data_Extractor remove <hero>-accuracy-v<N>`.
4. The session reports the pull request links and the final numbers to the user.

## Rules

- Commit messages and pull request bodies have no Co-Authored-By or other attribution lines.
- `git stash` is never run, in either worktree.
- The game is never launched, and no debugger is attached to it. The shipping binary is read by static disassembly
  alone.
- Each worktree is removed with `wt remove` once its pull request has merged.
