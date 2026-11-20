---
name: fellowship-datamine
description: Answers one question about a Fellowship game mechanic from the game's own data, working in a Fellowship_Data_Extractor worktree - the datamine export, the cooked assets and Blueprint bytecode, or the shipping binary by static disassembly. Returns the fact with its source. Use from /replay-accuracy when a coefficient, period, proc chance, cap, formula or interaction needs a source.
---

# Fellowship datamine

The prompt names an extractor worktree (`G:/source/Fellowship_Data_Extractor.<branch>`) and one question. The answer is
the fact with its source. All work is in that worktree, on its branch.

## Read first

- The worktree's `CLAUDE.md`.
- The worktree's `.claude/skills/datamining-fellowship/SKILL.md`: the rungs for this build, and the binary addresses
  decoded so far.
- The `extracting-fellowship-data` skill (Skill tool).

## Rungs

Each rung names the next. A miss on one rung is a reason to go to the next.

1. **The export.** `output/export-<build>/entities.jsonl`, the highest build with both `entities.jsonl` and
   `settings.json`. The `fellowship-codex` MCP tools read the main checkout's export: `find_entity`, then `get_entity`.
2. **The schema.** The usmap in `mappings/` lists every field a system has.
3. **The assets.** `dotnet run --project src/FellowshipExtractor -- dump <filter> [count]` writes every export of the
   matching cooked assets as JSON under `output/dump/`, and `dump-all <folder>` (a folder outside `output/`) writes the property trees, modifier
   magnitudes, Blueprint bytecode, constants, tag tables, curve tables, locres and config for every ability and effect.
4. **The binary.** `fellowship-Win64-Shipping.exe` in the Steam install, by static disassembly alone, with the harness
   the datamining skill describes. Each address decoded against an earlier build is re-anchored from its name literal.

A new reader in `src/FellowshipExtractor/Extraction/`, a new field on a document and a re-run of the export are
approved at any time. A fact the export lacks belongs on its document. The worktree's `CLAUDE.md` gives the commands.
Each change is committed on the worktree's branch, with no Co-Authored-By or other attribution lines.

## Rules

- The game is never launched. No debugger is attached, and nothing touches EasyAntiCheat.
- A number fitted to observed values is no answer. The answer is the value the game data or the code gives.
- `git stash` is never run.

## Answer

- The fact, in one sentence.
- Its source: an export field with its entity type and ID, an asset path with the property, a Blueprint function with
  the bytecode offset, or a binary address with the instructions.
- The value as SimFell writes it: the constant, its unit, and the comment naming the source.
- Each commit made in the worktree, with what it extracts.
