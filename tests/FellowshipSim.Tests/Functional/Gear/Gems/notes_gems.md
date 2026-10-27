# Gem tests: notes for test_description.md

Files (all in `Gear/Gems/`, over the six heroes through `GearHero.All`): `GemNodeUnlocking` (which nodes are active at which power), one class per colour (`GemRuby`, `GemAmethyst`, `GemTopaz`, `GemEmerald`, `GemSapphire`, `GemDiamond`), `GemLivePower` (power changes in a fight, a new fight, overcap), `GemSources` (`GemItem`), and `GemFights` (the builder and readers). 1,970 tests: 1,904 pass, 6 skipped, 60 fail on purpose (10 distinct tests, each on 6 heroes, plus the cases listed below).

Heroes are built **without a character sheet**, so the engine has to add each node's stats itself (a sheet already contains them: tested once in `GemRuby.A_Character_Sheet_Already_Includes_The_Gem`). A node is read as the difference between the gem power that unlocks it and the power just below.

## Failing on purpose (possible engine bugs)
- None. (Fixed on this branch: live gem power changes for Diamond, Ruby, Sapphire and Emerald, the overcap bonus when bound, and the flat rating nodes of a build without a sheet.)

## Skipped (need the game or a design decision)
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
