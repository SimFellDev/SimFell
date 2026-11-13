# Work package: dungeon affixes in a dungeon sim

A dungeon sim models dungeon affixes 24 (Anomalous Orbs) and 25 (Storm Shield). A dungeon's text names its affixes, the
dungeon import writes them from the log, and each Anomalous Orb is a priority target an action list can target.

## Base

- Repository `G:\source\SimFell`, on top of branch `gunde-apl-from-logs` with the changes of its worktree
  (`G:\source\SimFell.gunde-apl-from-logs`). The work uses three of them: the enemy line keys `first_seen` and `dies`,
  the action `switch_target`, and `debuff.rend.stack`.
- Read first: `CLAUDE.md`, `docs/ADDING_A_HERO.md`, `docs/REPLAY_ACCURACY.md` (rules of evidence), the dungeon text
  section of `README.md`.
- Game data comes from `G:/source/Fellowship_Data_Extractor.gunde-accuracy-v1` (build 26320967, read its `SKILL.md`),
  through a `game-data-authority` agent. Blueprint paths below are under
  `blueprints/Game/Modifiers/DungeonModifiers/<Affix>/<Asset>/` there.

## Target

### Dungeon text

- A dungeon key `affixes=` holds the run's affixes by name: `affixes=anomalous_orbs,storm_shield`. A text without the
  key has none, and a sim of it gives the result it gives today.
- `DungeonProfile` reads and writes the key, `DungeonProfile.Keys` lists it, and the dungeon editor, the language
  server and `README.md` have it.
- `DungeonReader.FromFight` writes the key from the fight's `dungeonstart` event (`RawEvent.Affixes`; the replay reads
  the same value into `ReplayScript`). It writes the affixes the engine models.

### Anomalous Orbs (affix 24)

- An Anomalous Orb is an enemy with `IsPriority` true, 0 Spirit Points and the health of creature 286 at the dungeon's
  health scale. `target.is_priority` is 1 on it, so `switch_target,target_if=target.is_priority` targets it.
- An orb casts Detonate from the moment it enters combat. `target.casting` is 1 on it during the cast, and an interrupt
  of the hero's stops the cast.
- An orb takes 4 times the damage while it is not casting. A hit of an effect whose calculation preset has
  `ApplyTargetIncomingDamageHealingMultipliers` off takes none of it.
- An orb's death gives the hero one stack of Anomalous Residue of the orb's gem colour.
- Orbs in the text: the import writes each orb of the log as an enemy line with `priority=1` and its own `first_seen`
  and `dies`, as it writes them today without the flag. With `anomalous_orbs` among the affixes, an enemy line named
  after creature 286 is an orb.
- Orbs by rule: with `anomalous_orbs` among the affixes and no orb line in the text, the engine spawns the orbs by the
  game's rule below. A hand-written dungeon needs the one key.

### Storm Shield (affix 25)

- The charge comes on the game's timer. Its bolt gives the hero Storm's Fury (20% crit chance, 20 s).
- The bolt's stun stops the hero's actions for its duration, once step 1 below confirms it in the logs.

### Values in Core

- The affixes' values are public constants in `FellowshipSim.Core`, each with its source in its comment, generated from
  the Codex export where the export has the record (the pattern of `FellowshipSim.Codex.Generators`).
- `PartyBuffs` and `AnomalousResidue` in `FellowshipSim.Engine/Replay` read those constants.

## Game data in hand

Each value is from table `DungeonModifiers` (the rows of `CT_DungeonModifiers`) or the named asset, build 26320967.

### Anomalous Orbs

| Fact | Value | Source |
| --- | --- | --- |
| Spawn timer | uniform 128 s to 148 s, running in combat alone, paused at run start | `AnomalousOrbs.SpawnTimeMin`, `SpawnTimeMax`; `GA_DungeonModifier_Passive_AnomalousOrbs` (ability 1949) ubergraph 1373, 1638, 6924, 6997 |
| Orbs a spawn | 3, each after its own 0 s to 3 s | `AnomalousOrbs.NumOfOrbs`, `Orb.RandomMaxDelayBetweenIndividualOrbSpawn`; offset 3447 |
| Spawn place, trash | around the closest enemy in combat within 700 of a random living player; bosses and affix actors are left out; with no such enemy the spawn is skipped | `PlayerToEnemyMaxDistance`, `SpawnMaxRadiusFromEnemy` 500; offset 1847, 3076 |
| Spawn place, boss fight | around a safe-arena marker or the player; orbs spawn in boss fights | offset 1847 |
| Gem colour | uniform over Amethyst, Diamond, Emerald, Ruby, Sapphire, Topaz | `Array_Random` over Gem Tags, offset 6110 |
| Creature | 286 Anomalous Orb, `BP_DungeonModifier_AnomalousOrbs_Orb` | creature record |
| Health | 2999 × 1.243 = 3727.757 at the base key, scaled as other enemies | `Default.BaseHealth`, `DungeonModifier_AnomalousOrb.Custom.DefaultBaseHealthMultiplier`; effect 155 |
| Score | KillScore 0, SpiritPointValue 0, no dungeon score | offset 6232 |
| Detonate | ability 1903: 15 s cast, interruptible, first cast at once | `Orb.Detonate.CastDuration`; AI phase DefaultCombat, InitialCooldown 0 |
| Recast | 12 s after an interrupt, 9 s after a crowd-control break; a stun holds the cast off | `Orb.Detonate.InterruptCooldown`, `Global.DefaultCCCooldown` |
| Cast end | the orb dies | `K2_OnEndAbility`, offset 1300 |
| Damage taken | effect 2711: `IncomingDamageMultiplier` MultiplyCompound 4, on while the orb is not casting | `Orb.BlockAllSkills.IncomingDamageMultiplier`; IgnoreTags `CharacterStatus.Internal.Casting` |
| Hits taking none of it | effects with preset `Empty`, `FlatTrue` or `FlatDamageHealingScaling`; of Gunde's, effect 3239 (Heart Splitter: Exsanguinate) and effect 3209 (Sundering Flesh) | `DT_EffectCalculationPresets`; binary gate 0x1450DB180, formula 0x145107B15 |
| Anomalous Residue | effects 2704 to 2708 and 2710: 480 gem power of the orb's colour a stack, 3 stacks at most, 60 s in the asset (the tooltip says 45 s) | `Orb.Buff.AdditionalGemPowerPerStack`, `MaxStacks`, `Duration` |
| Residue's receivers | every player within 6000 of the orb at its death by damage | ability 1902, `Orb.Buff.MaxRange` |

### Storm Shield

| Fact | Value | Source |
| --- | --- | --- |
| Timer | uniform 59 s to 72 s | `StormShield.SpawnPeriodMin`, `SpawnPeriodMax`; ability 1946 offset 4410 |
| Condition | every pair of players within 8000; otherwise a retry after 1.337 s | `StormShield.MaxTriggerDistance`; offset 16 |
| Charge | 8 s: effect 2700, and effect 2698 halving the damage the player takes | `StormShield.CountdownTimer` |
| Bolt | after the charge, effect 2701 on each player | offset 4677 |
| Bolt's stun | 2 s on each player hit (`CharacterStatus.BlockAllSkills`) | `MeteorRain.Fragment.StunDuration`; conditional effect of 2701 |
| Storm's Fury | effect 2699: crit chance Additive 0.20 for 20 s, one stack, refreshed by a new one, given to each living player after the bolt | `StormShield.CritBonusBuff.AdditionalCritChance`, `Duration`; offset 5217 |

### A run's affixes in the game

`DT_ModifersInDungeons` has a row for each dungeon and difficulty with the affixes of that run, and
`CT_ModiferSlotDiffcultyThresholds` the number of slots by difficulty (`CRFellowshipModifierSubsystem`,
`DefaultGame.ini`). The sim takes the affixes from the dungeon's text.

Eternal 27 is the game's difficulty 47 (`DifficultyScaling`), and the row `FrozenKingdom.47` (Cithrel's Fall) has
Anomalous Orbs and Storm Shield: the affixes of the logged run below.

### Further affixes changing a hero's damage

The affix classes take each of these by the same pattern (`DT_ModifierInfo`, the effect assets under
`/Game/Modifiers/`):

| Affix | Effect on a hero's damage |
| --- | --- |
| 8 Blood Shards | Imbued Blood (effect 55): Expertise Additive 0.01 a stack, 15 stacks, 30 s |
| 12 Empowered Minions | Victory Rush (effect 44) on a kill: Haste Additive 0.20 for 40 s |
| 13 Malevolent Shade | Protect (effect 38) on nearby enemies: 0.5 times the damage taken until the shade dies |
| 16 Ultimatum | CooldownReduction Additive 0.10 (effect 26); bosses' health 1.10 times (effect 27) |
| 18 Promoted Enemies, every run | promoted enemies have the health of one difficulty higher (effect 2121): 247.18 / 226.36 = 1.092 at Eternal 27, the ratio of four enemy types in the logged run |
| 19 Shadow Lord's Trial | Shadow's Defeat (effect 2221): DamageScale 1.2 and Haste 1.2 for 60 s at an Emissary's death |
| 21 Krumbug the Naughty | Sugar Rush (effect 2514): Haste 1.2 for 7 s; a friendly rat's damage at 10 procs a minute |
| 23 Pathfinder's Guidance | Haste by item level (effect 2703), 0.20 at most |
| 27 Truesight | Might of the Heskyr (effect 2843): DamageScale 1.2 for 50 s at a marked enemy's death |
| 28, every run of 4 heroes or more | DamageScale by the party's healers (effect 3198: 0.85, 1.00, 1.15, 1.35, 1.65 for 0 to 4); enemy health by its tanks (effect 3196) |
| 11 Binding Ice, 14 Meteor Rain | a root of 3 s, stuns of 4 s and 2 s on players |

## Logs in hand

Ab1yCxVcNBHPz6t8 fight 7 (Cithrel's Fall, Eternal 27, player 33, Gunde) has both affixes. Its files:

- Sim folder: `C:\Users\Sean\AppData\Local\Temp\claude\a8a9b2c708e0a306\17689be9-1d15-4f9e-803e-12385f833059\scratchpad\apl-from-logs\Ab1yCxVcNBHPz6t8_fight7_p33`
  (`dungeon.simc` is the import, `dungeon-fight.json` the import's input, `DefaultSlaughter.simc` Gunde's list).
- Sim against the log, with `<folder>` that path:
  `dotnet run .claude/skills/apl-from-logs/compare.cs -- <folder> <folder>/DefaultSlaughter.simc 200 8`
- Earlier harness runs with a test multiplier on orbs and a test Storm's Fury:
  `C:\Users\Sean\AppData\Local\Temp\claude\a8a9b2c708e0a306\17689be9-1d15-4f9e-803e-12385f833059\scratchpad\engineer2\final-list`
  (`J1-orb.out`, `J2-orb-storm.out`).
- The fight itself and 19 more Gunde fights of the patch are in the worktree's `data/fights`.

The log's numbers:

- **Orbs:** 27 orbs in 9 groups of 3, first seen at 2:19, 4:47, 7:25, 12:30, 14:51, 17:29, 20:19, 23:22 and 25:31. Each
  orb is in combat for 9 s to 24 s (the import's enemy lines).
- **Gunde's damage on orbs:** 8.8% of his damage, 20% of Slaughter's, 10% of Rend's. A Grim Carve, Blood Arc, Reaver's
  Edge or Bloodbound Spirit hit on an orb is 3.0 to 5.5 times the hit on another enemy of its pulse in 190 of 307
  pairs, and 0.8 to 1.3 times in 117. Exsanguinate's explosion on an orb is 0.8 to 1.3 times in 77 of 77 hits.
- **Storm's Fury:** on Gunde at 24% of his Attacks. Crit chance at his Attacks is 42.3% in the exact replay against
  36.9% in a sim.

The sim of that fight today, with no affix, against the log (200 fights):

| | Sim | Log |
| --- | --- | --- |
| Hero DPS | 51,315 | 51,975 |
| Exsanguinate average hit | 16,272 | 18,219 |
| Rend tick average hit | 4,665 | 5,427 |
| Slaughter tick average hit | 8,138 | 9,213 |
| Grim Carve average hit | 1,832 | 2,216 |
| Blood Arc average hit | 2,439 | 2,892 |
| Reaver's Edge average hit | 1,525 | 1,801 |

## Steps

1. **Measure the logs.** Over the downloaded fights with each affix, from the `dungeonstart` affixes and the events:
   - Orb groups: the seconds between groups in run time and in combat time, groups in boss fights, the orbs a group.
   - Each orb: its seconds in combat, the times of its Detonate casts and their stops, the share of its time with
     effect 1002711 on it, and the seconds from its first contact to the first stop.
   - Anomalous Residue on the hero: stacks, duration and uptime by colour, and the orb death giving each stack.
   - Storm's Fury on the hero: the seconds between applications in run time and in combat time, its length and
     uptime, and the hero's casts in the 2 s after each bolt.
2. **Close the game data questions** through `game-data-authority` (the binary run where the answer is native), and
   check each against step 1's numbers:
   - `CRAbilityTask_ModifierCooldown::WaitForModifierTrigger`: the four bools, the draw of each delay and its start,
     the repeat, the elapsed time over a pause, any pause in a boss fight or out of combat, `StartCustomCooldown`.
   - The health scale of an orb spawned through `CRAbilityTask_SpawnActorFromAbility`.
   - The moment a spawned orb enters `AIPhase.DefaultCombat`.
   - The event `Auth_KillCharacter` sends, deciding Residue from an orb dying at its cast's end.
   - `Global.AbilityPadding.Interrupt` and `Global.AbilityPadding.CC` (2 s each) on top of the 12 s and 9 s recasts.
3. **Core.** The affix classes with their constants; the `affixes` key in `DungeonProfile`; `DungeonFight` running each
   affix of the dungeon; an enemy's damage-taken multiplier in a sim (today `ReplayDriver` alone adds
   `StatType.IncomingDamageMultiplier` of the enemy's auras to `Hero.TargetDamageMultipliers`); the preset flag on the
   effects taking none of it; Residue through `Gem.ChangePower`; Storm's Fury as an aura of the hero.
4. **Orb behaviour.** The Detonate cast, its stop by the hero's interrupt, the recast delay, the death at the cast's
   end, and the party's stops. The party's stops and a rule-spawned orb's seconds in combat are party behaviour: take
   each from step 1 as a constant with its fights named, by the rules of evidence.
5. **Import.** `DungeonReader.FromFight` writes `affixes=` and `priority=1` on each orb line.
6. **Replay.** `PartyBuffs` and `AnomalousResidue` read the Core constants. The exact replay's numbers stay as they are.
7. **Editor, language server, README.** The dungeon editor's field for the affixes follows the editor's existing
   pattern for a dungeon value (the `building-ui-to-spec` skill).
8. **Gunde's list.** Measure `switch_target,target_if=target.is_priority` in a scratch copy of
   `Heroes/Gunde/ActionLists/Default.simc` against the log's share of Heart Splitter strikes on orbs. A change of the
   list itself goes to the user first.

## Checks

- `dotnet test -c Release` passes, with tests for: the key's text round trip; a dungeon without the key giving the
  same result JSON as before the change (1 thread, the `ab.cs` pattern in the scratchpad's `dungeon-lifetimes` folder);
  an orb's priority flag, cast, stop, recast and death; 4 times the damage while not casting and 1 times while
  casting; an effect with the flag off taking none; Residue's stacks, cap and duration; Storm's Fury's timer, crit
  chance and refresh; the bolt's stun; the import's `affixes=` and `priority=1`; the JSON round trip of each new
  contract value (`SimEngineContractTests`).
- On Ab1yCxVcNBHPz6t8 fight 7, the import again and 200 fights at 8 threads:
  - Orb groups a fight within 1 of the log's 9, and each orb's seconds in combat within the log's 9 s to 24 s.
  - Orbs' share of Gunde's damage within 2 points of 8.8%, of Slaughter's within 4 points of 20%, of Rend's within 2
    points of 10%.
  - Storm's Fury on at Gunde's Attacks within 3 points of 24%, and crit chance at his Attacks within 2 points of the
    log's for Grim Carve, Blood Arc, Reaver's Edge, Heart Splitter and Attack.
  - Hero DPS within 3% of 51,975.
  - The table above again with the affixes on, beside the log.
- A second fight with one of the affixes, of another dungeon in `data/fights`, gives the same checks its own numbers.
- Every constant names its source: a game data record, an asset property, a Blueprint offset, a binary address, or a
  log pattern holding in two fights with the fights named.

## Working rules

- Sims and tests run at 8 threads at most, one run at a time.
- The game is never launched, and the shipping binary is read by static disassembly alone.
- `git stash` is never run. Commit messages and pull request bodies have no attribution lines.
- The work goes to a branch and a pull request on `Seriousnes/SimFell` for the user's review; `gh` takes `--repo` on
  every call.
- Every enemy in combat is within reach of every ability and spawn rule, as in every sim.
