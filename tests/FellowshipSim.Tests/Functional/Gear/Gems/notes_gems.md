# Gem tests: notes for test_description.md

Files (all in `Gear/Gems/`, over the six heroes through `GearHero.All`): `GemNodeUnlocking` (which nodes are active at which power), one class per colour (`GemRuby`, `GemAmethyst`, `GemTopaz`, `GemEmerald`, `GemSapphire`, `GemDiamond`), `GemLivePower` (power changes in a fight, a new fight, overcap), `GemSources` (`GemItem`), and `GemFights` (the builder and readers). 1,970 tests: 1,904 pass, 6 skipped, 60 fail on purpose (10 distinct tests, each on 6 heroes, plus the cases listed below).

Heroes are built **without a character sheet**, so the engine has to add each node's stats itself (a sheet already contains them: tested once in `GemRuby.A_Character_Sheet_Already_Includes_The_Gem`). A node is read as the difference between the gem power that unlocks it and the power just below.

## Failing on purpose (possible engine bugs)
- **Ancient's Wisdom (Diamond) in a power change counts twice.** Reaching or losing the node in a fight (300 / 299, 1249 / 1250, 1000 / 1250) gives a Main Stat of 1,114.8 instead of 1,072.7 (rank I to II), 984.3 instead of 1,004 (losing it): the node's aura swaps *and* its stat modifier is added or inverted. `GemLivePower.A_Power_Change_Gives_The_Stats_Of_The_New_Power` (Diamond cases, 5 pairs).
- **Might of the Minotaur (Ruby) reached live from below 80 power** is not applied (the aura is registered but only applied at the start of a fight). Low impact (power below 80 is unrealistic). Same test, Ruby 79 to 80.
- **Blessing of the Commander (Emerald) does not follow a power change.** Reaching 450 or 1500 in a fight leaves the cooldown reduction as it was (`A_Power_Change_Moves_The_Cooldown_Reduction`).
- **Blessing of the Prophet (Sapphire) does not follow a power change**: the Spirit ability's cost and Spirit of Heroism's length stay (`A_Power_Change_Moves_The_Spirit_Ability_Cost_And_Duration`). Losing Ancestral Surge (power falling under 80) keeps its +10 maximum Spirit Points (`Losing_Ancestral_Surge_Lowers_Maximum_Spirit_Points`).
- **Overcap bonus is missing on a hero built from gem power.** Gem power over 1,500 adds 0.005% Main Stat per point, but the engine adds it only when the power changes in a fight; a gem bound at 1,600 has no bonus (`Gem_Power_Over_The_Cap_Adds_Main_Stat_When_Bound`). A character sheet has it already. The in-fight overcap (1,600 / 1,700, two colours adding) passes.

## Skipped (need the game or a design decision)
- **Flat rating nodes** (Berserker's Zeal, Thief's Alacrity, Vanguard's Resolve, Mystic's Intuition; +8 / +24 rating): the engine leaves them to a character sheet, so a hero built from gem power gets none, but a power change in a fight adds them (`GemLivePower.A_Rating_Node_Reached_In_A_Fight_Adds_Its_Rating` passes: the stat rises, its size is not asserted). To decide: should a build without a sheet get them? One skipped test per colour.
- **Might of the Minotaur only above 80% health** and **Blessing of the Conqueror only against a boss**: the engine does not model the hero's health and applies both from the start of every fight. Check in game what counts as a boss and whether the 80% is "above" or "at least".

## Followed the engine
- **Exactly 30% and 50% health** are not tested (Adrenaline Rush "30% or less", Sealed Fate "above 50%"); the tests use 29 / 31% and 49 / 51%.
- **Blessing of the Commander** cuts the cooldown of every ability in the engine; only the Major probe is read. TODO in the test: check which abilities it cuts.
- **Auras end on the server's frame**: Adrenaline Rush, First Strike and Spirit of Heroism are measured with a 20 to 50 ms tolerance (the engine ends them a frame, 17 ms, late).
- **Tariq's Wild Swing cannot crit** (the engine turns crit chance into damage), so the crit-chance-on-hit test for Sealed Fate reads the Core ability. It also ignores crit bonuses that depend on the target (Sealed Fate against a healthy enemy); not asserted, to check in game.

## Not covered
- Nodes that only heal, shield or defend (Unyielding Vitality, Titan's Blood, Reaper's Reprieve, Rogue's Resurgence, Sentinel's Bastion, Resonating Soul, Tranquil Resolve, Stoic's Teachings' Armor, Blessing of the Artisan's relic cooldowns): one test per colour checks that they leave damage unchanged; their own effects are out of the sim.
- The relics' gem power multiplier on a build (Covetous Greed): in the relics' tests (only `GemItem.PowerByColor` with 1.25 is here).
- Aspects that read a gem (Diamond Strike with Harmonious Soul, Emerald Judgement with First Strike): in the aspects' tests.
- Anomalous Residue itself (`FellowshipSim.Engine/Replay/AnomalousResidue.cs`): only the power change it causes (`Gem.ChangePower`).
- Harmonious Soul's rank I to rank II step keeping its stacks (a live power change with stacks up).
