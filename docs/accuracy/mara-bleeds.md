# Mara replay accuracy: mara-bleeds

The engine reproduces Mara's damage in the combat logs of patch 0.5.0.0 to the targets of `docs/REPLAY_ACCURACY.md`.
The baseline numbers are in `docs/accuracy/mara-bleeds.baseline.txt` and `docs/accuracy/mara-bleeds.baseline.json`.

## Fights

The measure runs on these 34 fights, one Mara player each:

```
ACCURACY_HERO=Mara ACCURACY_LABEL=final dotnet test tests/FellowshipSim.Tests -c Release --filter HeroAccuracyTests
```

| Report | Fight | Encounter | Player |
| --- | --- | --- | --- |
| 3M2vWr8taxVfTQFY | 53 | Xul, The Blood Monolith | Inkvi (143) |
| 3tM2NRFDCnhkqdx6 | 7 | Silken Hollow | beefy2 (1) |
| 79yJ4QqVgGpFA8vN | 1 | Crown of the Wild | jomako (2) |
| b649dhNptyW132Tm | 5 | Sin-Magir | Culex (80) |
| CwmX2Tb8GA6hyc1v | 8 | Wraithtide Vault | M3ch5ta (81) |
| d1LvbCpBWXRM8Hcm | 14 | Empyrean Sands | Culex (29) |
| dBgbn8wD1RxL7Kma | 9 | Empyrean Sands | Eon (1) |
| Fc8v6yWxD14GQMnV | 19 | Cithrel's Fall | Culex (77) |
| fjrCax7VzGmMZw6h | 26 | Cithrel's Fall | Leknok (23) |
| fx4ALNyWTkY3J8Za | 9 | Urrak Markets | Culex (2) |
| gd6ByNrf81jc9aWw | 3 | Silken Hollow | sbt (4) |
| GXFz3pZ94aQr2chV | 3 | Empyrean Sands | Faeyurae (1) |
| HgamNcX24tJ3qVQA | 21 | Stormwatch | Faeyurae (2) |
| Hwxc8TRFqPGhfjbN | 5 | Godfall Quarry | Faeyurae (31) |
| mThk4v1f8WGbBd7Q | 1 | Wraithtide Vault | windy-iwnl (4) |
| nJHTLzjY8yrQmCck | 5 | Godfall Titan | shrwd (3) |
| NXQnyLMF1aPrkCf2 | 5 | Ruins of Regath | jomako (3) |
| p8VJDcBdxa7ghqG3 | 124 | Cithrel's Fall | sbt (3) |
| Prvc4XmbdHBaVRKy | 12 | Cithrel's Fall | Gorak (76) |
| pWmcV42K39GHTLQX | 23 | Wraithtide Vault | sbt (88) |
| R2Bxhvkf3rc8FH4X | 1 | Scryer's Peak | M3ch5ta (2) |
| T2qLPpmMkrAtJ9B1 | 18 | Cithrel's Fall | sbt (2) |
| v8rhNxVc3PwDqzBA | 4 | Godfall Quarry | Victorvill (27) |
| v8rhNxVc3PwDqzBA | 4 | Godfall Quarry | Watanabe (28) |
| W4FMxpyqbnvaNYTc | 2 | Noor, The Betrayer / Ancient Koros / Cithrel | KOKOIA (3) |
| w7khnMKAa4TxXrmC | 23 | Godfall Quarry | windy-iwnl (5) |
| WcTBrQNX46m9axqP | 12 | Sin-Magir | Sorrel (65) |
| WcTBrQNX46m9axqP | 7 | Scrofa, Wild Incarnate | Sorrel (65) |
| X82J6bGRNBracxLg | 7 | Xul, The Blood Monolith | Qruza (2) |
| xkGbdm8aNDwTcgjF | 17 | Cithrel's Fall | sbt (3) |
| xkGbdm8aNDwTcgjF | 2 | Empyrean Sands | sbt (3) |
| za2QymCwxMcqfH16 | 20 | Wraithtide Vault | sbt (2) |
| zgxBpVKw2m71cDdk | 5 | Xul, The Blood Monolith | Faeyurae (45) |
| Zta3LQdvVbPJ9Whz | 6 | Empyrean Sands | beefy2 (27) |

The player is the log's actor ID, the `p<N>` of `data/fights/<report>_fight<fight>_p<N>.raw.json`.

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

### Unmodeled content

- **A1. Bloodrite Fervor** (ability 185, effect 1000156). The engine models it from its game data.
- **A2. Conjure Portal** (ability 1504). The engine models it from its game data.
- **A3. Rejuvenate** (ability 189). The engine models it from its game data.

### Resources and auras

- **A4. Trait : Focused Haste** (`Aura:1002141`). At every aura check of the fight set, the engine's stacks equal the
  log's, within `ReplayOptions.AuraToleranceSeconds`.
- **A5. Energy** (`Resource:Energy`). The engine's Energy is within 3.0 of the log's at every resource check of the
  fight set (the Energy entry's `Tolerance` in `LogResources`).
- **A6. Inspired Allegiance** (`Aura:1002112`). At every aura check of the fight set, the engine's stacks equal the
  log's, within `ReplayOptions.AuraToleranceSeconds`.

### Fights

- **A7. fjrCax7VzGmMZw6h fight 26, Cithrel's Fall, Leknok (23).** Exact replay damage / log is 99% to 101%.
- **A8. gd6ByNrf81jc9aWw fight 3, Silken Hollow, sbt (4).** Exact replay damage / log is 99% to 101%.

## Evidence

Each item is met by a change whose every value names its source, by the rules of evidence in
`docs/REPLAY_ACCURACY.md`: game data first, a log pattern holding in at least two fights and over every occurrence in
them, and a source for every constant, multiplier, period, chance and cap. An item the work leaves off its target has a
line in `src/FellowshipSim.Core/Heroes/Mara/NOTES.md` under "Not yet explained", with its final numbers and every source
checked. A constant chosen to make a number match meets no item.

## Checks

`dotnet test -c Release` passes.
