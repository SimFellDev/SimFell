# Gunde replay accuracy v1

Gunde's replays meet the targets of `docs/REPLAY_ACCURACY.md` over the fights of patch 0.5.0.0 below. The baseline
numbers are in `docs/accuracy/gunde-accuracy-v1.baseline.txt` and `docs/accuracy/gunde-accuracy-v1.baseline.json`.

## Fights

The final measure runs on these 20 fights:

```
ACCURACY_HERO=Gunde ACCURACY_LABEL=final dotnet test tests/FellowshipSim.Tests -c Release --filter HeroAccuracyTests
```

| Report | Fight | Encounter | Player |
| --- | --- | --- | --- |
| 2NwV9b6jdhFnzXJk | 2 | Silken Hollow | M. |
| 67qwcRKCWYFzyxPA | 6 | Godfall Quarry | Jello |
| 7w6mAjpTVgD14aHQ | 11 | Stormwatch | Chudalingus |
| 7w6mAjpTVgD14aHQ | 12 | Godfall Quarry | Chudalingus |
| 8vb2pTAhFQRcktaD | 2 | Wraithtide Vault | Solomio |
| Ab1yCxVcNBHPz6t8 | 7 | Cithrel's Fall | Azif |
| AFan1VtPvjYW3y7K | 4 | Empyrean Sands | M. |
| CPNgLQD4Htynkh2G | 6 | Wraithtide Vault | M. |
| CwmX2Tb8GA6hyc1v | 8 | Wraithtide Vault | Chudalingus |
| DrabZB8wYcQ4fx6m | 33 | Xul, The Blood Monolith | TTV TheSpryFrog |
| DYL3Ph6Fkgp8jVbw | 1 | Godfall Quarry | M. |
| h7FpWxARG61jt9yD | 1 | Cithrel's Fall | coffee |
| Hzc78X3TFPvMVnhb | 3 | Cithrel's Fall | Snelhest |
| PFtrX9RZJxmv1QfT | 2 | Cithrel's Fall | Pandasama |
| VP9bnawhd6prjfKN | 6 | Wraithtide Vault | ttvBIGtime |
| VP9bnawhd6prjfKN | 8 | Urrak Markets | ttvBIGtime |
| WLxVtYNH93cgM6jC | 12 | Silken Hollow | Azif |
| XQHxvNmwRp8TcMJG | 2 | Xurath, Fist of the Warden | Solomio |
| XQHxvNmwRp8TcMJG | 3 | Overlord Varux | Solomio |
| z4btLWmnaZHfCYVN | 9 | Scrofa, Wild Incarnate | 鼠鼠滚 |

## Headline targets

| Measure | Target |
| --- | --- |
| Exact replay damage / log, each fight | 99% to 101% |
| Free dice mean / log, all fights | 98% to 102% |
| Mean log percentile in free dice | 30% to 70% |
| Calibration (mean log/engine), normal and crit, each damage ID over 1% of the log's damage | 0.98 to 1.02 |
| Log hits matched | 98% or more |
| Engine-only hits, each damage ID over 1% of the log's damage | 2% of its log hits or fewer |

## Items

### Focus

- **F1.** Rend's share of each Gunde damage source. Rend's debuff stacks (1003231) are its total damage in Strength, so
  the share of a hit's damage added to Rend is the rise in Rend's debuff stacks on the enemy at that hit. For every
  Gunde damage ID of the fights above, in Reign in Blood (1003241) and out of it, the engine adds to Rend the share the
  logs show. The damage IDs are Exsanguinate (1003239), Rend's tick (2286), Slaughter's ticks (1003237), Grim Carve
  (2262), Blood Arc (2291), Heart Splitter (2294), Reaver's Edge (2290), Rupture (2293), Fated Strike (160),
  Bloodbound Spirit (2296), Attack (1249), Emerald Judgement (1000093), Double Strike (2301), Warbound (2299) and
  Butcher's Hook (2284). Owed in Blood's cast (2298) adds to Rend the rise in Rend's debuff stacks the logs show at
  that cast. Each share holds in at least two of the fights above.
- **F2.** Rend after a Slaughter, in a sim. A sim of Azif's character on the logged run's dungeon (Ab1yCxVcNBHPz6t8
  fight 7, player 33, Cithrel's Fall), 200 iterations, with Gunde's `ActionLists/Default.simc` as it is in the
  worktree and Slaughter cast about 1.2 times a minute against the log's 1.15, has each of these average hits within 5%
  of the log's:

  | Damage ID | Log average hit |
  | --- | --- |
  | Exsanguinate (1003239) | 18,219 |
  | Rend's tick (2286) | 5,427 |
  | Slaughter (1003237) | 9,213 |

  The sim's inputs are in
  `C:\Users\Sean\AppData\Local\Temp\claude\a8a9b2c708e0a306\17689be9-1d15-4f9e-803e-12385f833059\scratchpad\apl-from-logs\Ab1yCxVcNBHPz6t8_fight7_p33`,
  and this command runs it, with `<folder>` that path:

  ```
  dotnet run .claude/skills/apl-from-logs/compare.cs -- <folder> <folder>/DefaultSlaughter.simc 200 8
  ```
- **F3.** Mechanics the engine keeps:
  - Exsanguinate (1003239) deals 30% of the Rend damage still to deal on the target and leaves the Rend unchanged.
  - Slaughter (2292) consumes every Rend stack on each enemy hit and applies one Slaughter bleed (1003237) per enemy:
    160% of that enemy's consumed Rend damage over 3 s, multiplied by `Rupture.OpenWoundsSlaughterDamage` on an enemy
    with Open Wounds (1003233).
  - Rend builds from the damage Gunde's abilities deal, with no fixed rate.

### Unmodeled content

The engine models each of these from its game data.

- **A1.** Bloodrite Fervor (185, effect 1000156).
- **A2.** Conjure Portal (1504).
- **A3.** Rejuvenate (189).
- **A4.** Firing Cannon (557).

### Calibration

- **A5.** Exsanguinate (1003239): normal calibration 0.98 to 1.02.
- **A6.** Slaughter (1003237): normal calibration 0.98 to 1.02.
- **A7.** Grim Carve (2262): normal and crit calibration 0.98 to 1.02.
- **A8.** Heart Splitter (2294): normal and crit calibration 0.98 to 1.02.

### Auras

Before each cast, the engine's aura has the log's stacks within `ReplayOptions.AuraToleranceSeconds` (0.1 s).

- **A9.** Trait : Focused Haste (1002141): equal stacks.
- **A10.** Inspired Allegiance (1002112): equal stacks.
- **A11.** Owed in Blood (1003245): equal stacks.
- **A12.** The Wayfarer (1003184): equal stacks.
- **A13.** Crimson Strikes (1003224): equal stacks.
- **A14.** Reckless Abandon (1003247): equal stacks.
- **A15.** Harmounious Soul - Medium (1001960): equal stacks.

## Evidence

Each item is met by a change whose every value names its source, by the rules of evidence in `docs/REPLAY_ACCURACY.md`:
a game data record, an asset property, a Blueprint function, a binary address, or a log pattern holding in at least two
fights with those fights named. An item the change does not meet has a line in
`src/FellowshipSim.Core/Heroes/Gunde/NOTES.md` under "Not yet explained", with its final numbers and every source
checked. A constant chosen to make a number match meets no item.

## Checks

- `dotnet test -c Release` passes.
- By the user's direction, `Default.simc` follows Azif's play in Ab1yCxVcNBHPz6t8 fight 7 in two actions: its first
  action, `switch_target,target_if=target.is_boss`, sets the target to a boss in combat, as each of Azif's 232 Heart
  Splitter strikes with a boss in combat has, and its Bleeding Hearts Slaughter action has the conditions of Azif's 30
  Slaughter casts. Every other action of Gunde's action lists in `src/FellowshipSim.Core/Heroes/Gunde/ActionLists/`
  is as it was at the start of the work.
- The work's changes are uncommitted in the worktree for the user's review.
