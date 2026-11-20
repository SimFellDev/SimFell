---
name: replay-accuracy-review
description: Use in a new session when invoked as /replay-accuracy-review <branch>, or when asked for an adversarial review of a <hero>-accuracy-v<N> branch. Stage 3 of docs/REPLAY_ACCURACY.md - checks every value, claim and replay change on the branch against evidence this session gathers itself, and writes docs/accuracy/<branch>.review.md.
argument-hint: <hero>-accuracy-v<N>
---

# Adversarial review of a replay accuracy branch

The argument is a branch, `<hero>-accuracy-v<N>`. The session runs on Fable 5.1 at xhigh effort, a model other than
the implementation's. Its `game-data-authority` runs keep their definition's Opus 5.5 at high effort. This session takes each claim on the branch as false until evidence
it gathers itself shows it true. A claim is every value the branch adds to the engine, every sentence it adds to a
`NOTES.md`, every source a comment or commit message names, and every number in the pull request body.

The session changes no engine, replay or test code. Its output is `docs/accuracy/<branch>.review.md`.

## The one hero

`<hero>` is the first part of the branch name, and `<Hero>` is that hero's folder in `src/FellowshipSim.Core/Heroes`.
The review covers `<Hero>` alone: `<Hero>`'s players in the spec's fights, `<Hero>`'s `NOTES.md`, and the branch's
changes as they act on `<Hero>`. Within a fight, only the events of `<Hero>`'s players are read; `dotnet test -c
Release` is read as pass or fail. Every prompt for an agent of the review names `<Hero>` and states this. An agent
whose answer names or reads any other hero, however briefly, is stopped, and a new run asks its question again. Text
on the branch that names any other hero is a finding of kind **contamination**, and the review fails.

## Setup

1. The SimFell worktree is `G:/source/SimFell.<branch>`. Its `data/fights` has the spec's fights. The session works
   there: `EnterWorktree` is not used, since the worktree exists; commands run with that path as their directory.
2. The review's extractor worktree comes from the extractor's `main`, so its history has none of the implementation's
   readers: `wt -C G:/source/Fellowship_Data_Extractor switch --create <branch>-review --no-cd`. Its path is
   `G:/source/Fellowship_Data_Extractor.<branch>-review`. It is removed with `wt remove -D` at the end; nothing from it
   is pushed.
3. The inputs: the spec `docs/accuracy/<branch>.md`, `git log origin/main..<branch>`, `git diff origin/main...<branch>`,
   the pull request body (`gh pr view <branch> --repo Seriousnes/SimFell --json body --jq .body`), and `docs/REPLAY_ACCURACY.md`. The
   implementation session's transcript is no input.

## Checks

### 1. Fitted constants

Each numeric literal the diff adds or changes in `FellowshipSim.Core` or `FellowshipSim.Engine` is a candidate. For
each:

- Its subject's log/engine ratio in `docs/accuracy/<branch>.baseline.json`: a value equal to that ratio, to its inverse,
  or to a product of either with a known multiplier, is a fitted constant until the next step shows a source.
- A fresh `game-data-authority` agent, started in the review's extractor worktree, gets `<Hero>` as the one hero, the
  subject and the question, with no value and no claimed source: "What is the coefficient of X in the game data, and where is it?" Its answer is compared
  with the diff's value. The agent sees neither.
- A source named as a log pattern is recounted by this session from `data/fights`, over every fight the comment or
  `NOTES.md` names.

A value with no source in the game data and no log pattern this session recounts is a finding of kind
**fitted constant**.

### 2. Claims in NOTES.md and commit messages

Each sentence the branch adds to a `NOTES.md` and each claim in a commit message is checked against the fights it names,
by counting every occurrence in them. A claim the counts contradict, or one with no fights named, is a finding of kind
**unsubstantiated claim**.

### 3. Replay changes that hide differences

Each change in `FellowshipSim.Engine/Replay` is read for its effect on the measure: a widened tolerance in
`ReplayOptions`, an ID added to `UnverifiedDamageIds`, a fight or event left out, a pairing rule that matches more log
hits to fewer engine hits. A change whose evidence is absent or does not hold over `<Hero>`'s fights is a finding of kind
**masking**.

### 4. Logic

The diff is read for errors in the mechanics it models: a talent's effect outside its condition, a stack count, haste
on a period that the game data gives unhasted, a cooldown or charge rule, an event's order, a multiplier applied twice
or to the wrong damage. Each is a finding of kind **logic error**, with the line and the game data or log evidence.

### 5. Numbers

The session runs the measure on the spec's fight set:

```
ACCURACY_HERO=<Hero> ACCURACY_LABEL=review ACCURACY_FREEDICE_ITERATIONS=100 dotnet test tests/FellowshipSim.Tests -c Release --filter HeroAccuracyTests
```

and `dotnet test -c Release`. A number in the pull request body that the measure does not reproduce is a finding of
kind **wrong number**, and a failing test one of kind **regression**.

## Review file

`docs/accuracy/<branch>.review.md` has:

- one section per finding: its kind, `file:line`, the claim, the evidence this session gathered, and what the evidence
  gives in its place;
- a table of every value checked under check 1, with its source as this session found it;
- the measure's headline numbers from check 5.

The session commits the file on the branch and pushes it, with no attribution lines, and reports the number of
findings by kind and the review file's path to the user. Fitted constants and masking come first in the report.
