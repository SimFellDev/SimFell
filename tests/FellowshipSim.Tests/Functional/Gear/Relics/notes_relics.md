# Relic passives: notes

Files: `RelicFights.cs` (helpers), `RelicPassives.cs` (all tests, theories over the six heroes through `GearHero.All`).
93 test cases: 81 pass, 12 fail on purpose.

## Failing (on purpose)
- **Gilded Broach's +5% Main Stat** (2 theories x 6 heroes): the engine has the relic as defensive only (no Main Stat). Missing feature.
  Assumption to check: the +5% multiplies Main Stat like the other percentage Main Stat buffs (the test reads a ratio of 1.05 on a bare hero).
  If the game adds it to another percentage bonus (Rising Spirit's sum, say), the ratio is the same alone but differs once other bonuses are up.

## Skipped
None.

## Followed the engine
- Dragonbone Dice and Undying Heart durations (14s, 20s) and chances (0.9 and 0.8 PPM scaled by haste) are the engine's values (tooltip + logs); the
  game data's 5-second cooldown between procs is not modelled (logs show it does not hold), so no test for it.
- Accursed Trophy is tested as +20% on the Main Stat pool ("More" PrimaryAttribute); both the Main Stat and the damage ratio read 1.2 exactly.

## Notes
- Tariq's Wild Swing never crits (its hits are `canCrit: false`; his crit chance only feeds its Recklessness), so the Dragonbone tests cast Heavy Strike for him.
- Two relics at once: Accursed Trophy + Undying Heart both apply.
- Protective enchants (every ID in the catalog) change no damage (checked on Rime and Tariq); the common abilities (Revive, Anchor, ...) are on every hero and casting Anchor deals no damage.

## Not covered
- Gilded Broach's heal (below 20% health, 40% of max health, once per 180s): defensive, no health model in the step driver.
- Covetous Greed (+25% gem power): in the shared-mechanics package.
- Whether Dragonbone Dice / Undying Heart have a 5s internal cooldown in the game (the logs say no).
- Common abilities other than Anchor's cast (Making Camp, Mount, Relic Teleport, Major Invisibility, Chickenize, Major Dispel, Revive): unverified in the engine, no damage.
