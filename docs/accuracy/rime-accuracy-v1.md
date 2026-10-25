# Rime replay accuracy v1

Rime's replays meet the targets of `docs/REPLAY_ACCURACY.md` over the fights of patch 0.5.0.0 below. The baseline
numbers are in `docs/accuracy/rime-accuracy-v1.baseline.txt` and `docs/accuracy/rime-accuracy-v1.baseline.json`.

## Fights

The final measure runs on these 15 fights:

```
ACCURACY_HERO=Rime ACCURACY_LABEL=final dotnet test tests/FellowshipSim.Tests -c Release --filter HeroAccuracyTests
```

| Report | Fight | Encounter | Player |
| --- | --- | --- | --- |
| 4Ln6PQZCRzc7Wpta | 77 | Stormwatch | Slick |
| 4Ln6PQZCRzc7Wpta | 78 | Wraithtide Vault | Slick |
| AY1m6jPKTZyx4vz2 | 9 | Godfall Quarry | Splite |
| D1fBhVPkwYKXWbpr | 1 | Cithrel's Fall | Wobra |
| GWChJ8P7tn6fHgV3 | 20 | Godfall Quarry | Xaxina |
| GWChJ8P7tn6fHgV3 | 22 | Empyrean Sands | Xaxina |
| jHDC613ZzbTYR8wW | 18 | Silken Hollow | Loreuxe |
| N18KtgP3rvpYGm24 | 27 | Urrak Markets | Zya |
| NhBnFXmy3bJg9Cav | 32 | Crown of the Wild | inmy |
| NhBnFXmy3bJg9Cav | 33 | Wraithtide Vault | inmy |
| Rzg1nQNHpm2fhYtB | 23 | Noor, The Betrayer / Ancient Koros / Cithrel | sww |
| Rzg1nQNHpm2fhYtB | 24 | Xurath, Fist of the Warden | sww |
| XNpG3AYWM8rq2zwx | 19 | Empyrean Sands | watts |
| XNpG3AYWM8rq2zwx | 20 | Scryer's Peak | watts |
| ZPTw4tmBJnK7hCb1 | 4 | Vexira, Mother of Nightmares | Wobra |

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

The engine models each of these from its game data.

- **A1.** Ice Dash (1024).
- **A2.** Brain Freeze (1019).
- **A3.** Frost Ward (1015, effect 1001379).
- **A4.** Frigid Winds (1021, effect 1001385).
- **A5.** Mount Indomitable Chicken (2051).
- **A6.** Mount Brum the Winter Warmth (1812).
- **A7.** Mount Inflatable Shark (2047).
- **A8.** Chickenize! (1478).
- **A9.** Mount Nightveil Widow (1683).
- **A10.** Major Dispel (187).
- **A11.** Revive (22).
- **A12.** Mount Spirit of Akari (1897).
- **A13.** Mount Vilesoul Raptor (1900).
- **A14.** Relic Teleport (1506).
- **A15.** Anchor (597).
- **A16.** Mount Aurellian Swiftpaw (2049).
- **A17.** Mount Cheesers (1774).
- **A18.** Making Camp (959).
- **A19.** Major Invisibility (1486, effect 1002146).
- **A20.** Augmentation Behemoth's Might / Jotunn's Guille / Titan's Cunning (3000065), at every rank.
- **A21.** Augmentation Sun's Might / Star's Guille / Moon's Cunning (3000066), at every rank.
- **A22.** Augmentation Revenant's Might / Wraith's Guille / Banshee's Cunning (3000067), at every rank.
- **A23.** Augmentation Gryphon's Might / Phoenix's Guille / Sphinx's Cunning (3000068), at every rank.
- **A24.** Augmentation Ursa's Might / Wolf's Guille / Vampire's Cunning (3000069), at every rank.
- **A25.** Augmentation Manticore's Might / Basilisk's Guille / Spider's Cunning (3000070), at every rank.
- **A26.** Diamond gem node Harmonious Soul (node 338).
- **A27.** Diamond gem node Harmonious Soul - II (node 339).
- **A28.** Legendary item 5900, Northlander's Wraps.

### Calibration

- **A29.** Amethyst Splinter (1000101): normal calibration 0.98 to 1.02.
- **A30.** Coalescing Frost (1002300): normal and crit calibration 0.98 to 1.02.
- **A31.** Talon Strike (2223): normal and crit calibration 0.98 to 1.02.
- **A32.** Bursting Ice (1001396): normal and crit calibration 0.98 to 1.02.
- **A33.** Frost Swallows (1033): normal and crit calibration 0.98 to 1.02.
- **A34.** Glacial Blast (1028): normal and crit calibration 0.98 to 1.02.
- **A35.** The Vehement's Disdain (1003193): normal calibration 0.98 to 1.02.
- **A36.** Cold Snap (1020): normal and crit calibration 0.98 to 1.02.
- **A37.** Frost Swallows (1001365): normal and crit calibration 0.98 to 1.02.

### Hit counts

Each damage ID has matched hits of 98% or more of its log hits, and engine-only hits of 2% of its log hits or fewer.

- **A38.** The Vehement's Disdain (1003193).
- **A39.** Ice Comet (2227).
- **A40.** Bursting Ice (1001396).
- **A41.** Frost Swallows (1033).
- **A42.** Coalescing Frost (1002300).
- **A43.** Amethyst Splinter (1000101).
- **A44.** Freezing Torrent (1027).
- **A45.** Rising Talons (2224).
- **A46.** Frost Swallows (1001365).
- **A47.** Talon Strike (2223).
- **A48.** Chronoshift (1558).

### Resources and auras

Before each cast, the engine's value agrees with the log's within the replay's tolerance: a resource within its
`Tolerance` in `LogResources.All`, an aura with equal stacks within `ReplayOptions.AuraToleranceSeconds` (0.1s).

- **A49.** Anima (resource type 2): within 0.99.
- **A50.** Spirit (resource type 4): within 2.0.
- **A51.** Winter Orbs (resource type 5): equal.
- **A52.** Harrowing Ice (1002839): equal stacks.
- **A53.** Hidden Power (1002164): equal stacks.
- **A54.** Seized Opportunity (1002111): equal stacks.
- **A55.** The Wayfarer (1003184): equal stacks.
- **A56.** The Vehement (1003194): equal stacks.
- **A57.** Frostwyrm's Spite (1002264): equal stacks.
- **A58.** Seized Opportunity (1002110): equal stacks.

## Evidence

Each item is met by a change whose every value names its source, by the rules of evidence in `docs/REPLAY_ACCURACY.md`:
a game data record, an asset property, a Blueprint function, a binary address, or a log pattern holding in at least two
fights with those fights named. An item the change does not meet has a line in
`src/FellowshipSim.Core/Heroes/Rime/NOTES.md` under "Not yet explained", with its final numbers and every source
checked. A constant chosen to make a number match meets no item.

## Checks

- `dotnet test -c Release` passes.
