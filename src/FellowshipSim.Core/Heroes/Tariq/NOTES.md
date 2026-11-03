# Tariq: what the logs showed

How Tariq's mechanics were pinned down, beyond the tooltips. Each point is also a comment next to the code it shapes.
Measured on 3 Tariq logs of 2026-10-23 (fXyzwGcQkTrLD34R fight 4 Stormwatch, 1p7AXmHfwV9CYMxR fight 30 and
YC861NTRgLbmw4Qa fight 31 Cithrel's Fall) unless noted. Game values are from game data build 26136736.

## Damage

- **Damage IDs:** Skull Crusher's thunder bolt is logged under 1229 and Hammer Storm's lightning under 1242, as their
  physical hits are. Heavy Strike is three effects: 1776 on the target, 2755 on the other enemies, 1779 for the
  lightning on all of them. Face Breaker's splash is 2241, Sledgehammer 2061, Chain Lightning 1774, Raging Currents
  1767, Raging Tempest's leap 1231 and Leap Smash 1225.
- **Hammer Storm:** 3 spins, the first one server frame after the cast, the others every hasted half second from the
  cast itself (on the server frame after it), not from the first spin. Each spin deals 1.35 times the one before.
  Every enemy's physical hit comes first, then every enemy's lightning.
- **Face Breaker's splash:** exactly 50% of the hit dealt to the target, crit included. It never crits and loses damage
  as sqrt(12/N) beyond 12 enemies.
- **Sledgehammer:** exactly 10% of the physical Skull Crusher hit dealt (the talent's text; ability 2061 says 30%). It
  never crits.
- **Culling Strike:** 40% more damage per Fury spent, up to 10 Fury. Executioner's Grin counts as 30 Fury spent (the
  legendary's text; the effect's says 20).
- **Leap Smash:** +40% crit chance of its own (placeholder constant `Talent.IncreasedCriticalChance.AddedCrit`). It
  crits 72-86% of the time against 35-44% for Face Breaker.
- **Crack the Sky and Heavy Strike:** Thunder Call: Heavy Strike's lightning (1779) does not gain Crack the Sky's +20%
  crit chance, though the tooltip says all lightning damage does. In 4 fights with the talent (wv4ZLFxN8zGTW6M9 fight 7,
  JW7pzaBxMd9bNCZK fight 112, NjMn2cahDP9fWBA7 fight 39, qVtpywQ9B6RCab3A fight 5; 2,252 hits each side) the lightning
  crits 44.5% against 43.3% for the same casts' physical hits, while Chain Lightning crits 56-68% and Raging Currents
  51-75%. This is a developer bug in the game (reported by a player); the engine models the game's current behaviour in
  Heavy Strike's class alone until the game fixes it. Whether Skull Crusher's and Hammer Storm's lightning gain the
  talent is not known: they share their damage IDs with the physical hits.
- **The Motherload:** each other enemy a Chain Lightning hits adds about +50% to its later hits, not the tooltip's +65%
  (q3y7pWgXrjN2dftw fight 12: the median non-crit hit by bounce in 182 clean 9-hit chains is 1 + 0.49 times the first).
  The game data has only the tooltip. Taken as a developer bug the engine models (UNVERIFIED, one fight): free dice of
  the fight go from 126.9% of the log's Chain Lightning damage to 107.2%.
- **Free Chain Lightning:** Ace of Spades' and Blood & Thunder's land their first hit 0.50s after the hit or cast that
  procced them (0.30s after a cast). Ace of Spades' starts on the enemy the proccing hit struck; Blood & Thunder's mostly
  on another (26 of 34). Bounces land on the server frame at or after each 0.05s from the first hit, 2 and 1 frames
  apart in turn (q3y7pWgXrjN2dftw fight 12).
- **Them Bones:** grievous Skull Crusher crits come with the thunder bolt as well as the physical hit.
- **Schism:** each direction (Skull Crusher's roll for Hammer Storm, Hammer Storm's for Skull Crusher) has bad luck
  protection of its own. After 0, 1 and 2 misses in a row the proc rate is 16-19%, 31-35% and 44-46%: 15.8% a step,
  whose average is the tooltip's 35% (17 fights, 5265 rolls). Log-likelihood: a counter for each -3246, one counter for
  both -3610, a flat 35% -3388. Past 3 misses the rate climbs slower (56%, 60%, 63-73%), and 19 rolls miss after 7 or
  more, where the chance is 100%: a proc the replay does not find in the log leaves the count high.
- **Wild Swing:** never crits. Enemies avoid 27 of its 388 hits (3-10% a fight; 22 misses, log hit type 0, and 5
  dodges, type 7; no damage), and no other Tariq hit.

## Timing

- **Skull Crusher's global cooldown:** 1s before haste (`GE_GlobalCooldown_TariqHeavySingleTarget`). The next cast
  comes 2/3 of the usual global cooldown after it.
- **Off the global cooldown:** Culling Strike (its ability has no global cooldown effect; casts come 5ms after others),
  and the abilities whose tooltip says so.
- **Hasted cooldowns:** Skull Crusher, Hammer Storm and Wild Swing, as well as the ones the game data lists. Skull
  Crushers come 0.835s apart at 20% haste.
- **Leap Smash** deals its damage 0.27-0.30s after the cast; Mouth for War's Focused Wrath stack and High Road's Fury
  come with it. **Raging Tempest's leap** deals its damage 0.30-0.46s after the cast.
- **Chain Lightning:** the first hit 0.30s after the cast, then a hit every 0.05s. It hits enemies it has not hit
  yet first.
- **Raging Tempest:** the buff comes with the cast at 1 stack and lasts 20s from the cast. Each Raging Currents hit adds
  a stack and leaves the time left as it is. Raging Currents strikes every hasted second from the cast. Raging Tempest
  removes Thunder Call as it starts.

## Resources

- **Fury** is logged as resource type 2, a share of a maximum that follows Strength. The replay reads it as a share of
  100. Tariq loses none of it between pulls.
- **Bloodline** generates 1 Fury every second, not hasted (confirmed in game: 50 Fury over 50 seconds, in smaller
  fractional ticks; the engine keeps one tick a second, as the game data has it), in combat alone (1p7AXmHfwV9CYMxR
  fight 30: no Fury between pulls). The logs disagree: while Tariq casts nothing they show whole 1-Fury steps on a grid
  of about 1 / (1 + haste) seconds (NhBnFXmy3bJg9Cav fight 6, 301.0-320.6: 25 Fury in 19.5s at about 27% haste; about
  40 quiet stretches in 15 fights fit that grid and not a 1-second one, and it follows haste, not Strength). The in-game
  timing overrules them: the logs' Fury is unreliable.
- **Ride the Lightning** generates Fury on ticks hasted by the haste as Thunder Call is cast, for the whole buff
  (confirmed in game). Its Fury comes in Raging Tempest too (fight 31 @ 199.7: about 2.2 Fury a second, with Thunder
  Call gone).
- **Kill Em All** never procs from Heavy Strike (confirmed in game; 3 fights with the talent: none of 537 Heavy Strikes,
  where 10% would be about 54).
- **Executioner's Grin** rolls on every cast, the weapon's too (fight 4 @ 66.7: Fated Strike).

## Replay matches

| Fight | Hits matched | Damage of log |
|---|---|---|
| fXyzwGcQkTrLD34R fight 4 | 5473/5676 | 103.0% |
| 1p7AXmHfwV9CYMxR fight 30 | 6186/6438 | 101.1% |
| YC861NTRgLbmw4Qa fight 31 | 13529/13698 | 101.6% |

Them Bones is proven by its grievous Skull Crusher hits (log hit type 22): 35, 65 and 96 procs against 33, 71 and 101
expected. Wild Swing's misses and dodges are proven by its hits of types 0 and 7: 11, 8 and 4 against 9, 8 and 10
expected.

## Game data against the logs

- **Sledgehammer:** the talent says 10%, its ability 2061 says 30%. The logs have 10% (587 hits).
- **Face Breaker's splash:** ability 2241 says 25% of a crit's damage (Left Hand Path's text names +40% crit chance
  instead). The logs have 2241 at 50% of every Face Breaker hit, crit or not, without Left Hand Path: it is Face
  Breaker's own splash. Left Hand Path itself is in no log.
- **Skull Crusher's 1.2:** its placeholders carry `Talent.IncreasedDamageScaler 1.2`, with no talent naming it. With
  Them Bones proven, Skull Crusher matches the logs within 3% without it (0.98, 0.98 and 1.01), so the engine leaves it
  out.
- **Hammer Storm's 35% per spin:** the spins deal 1.00, 1.37 and 1.88 times the first (fight 4 @ 23.36): each
  multiplies the one before (1.35 and 1.82). An added 35% per spin would give 1.35 and 1.70.

## Known gaps

- **Spirit Points from damage** (the shared formula: damage / max health x the creature's points) run high. Spirit
  regeneration matches the log (+1 every 3.0s).
  - Each Spirit gain in the log, set against the hit of the same moment, is 0.3-0.8 of the formula's, creature by
    creature, in fights 4 and 30.
  - Emissary of the Shadow Lord (game data: 400 points) gives 367 Spirit Points in fight 30 by the formula and about 6
    in the log.
  - Over a whole fight (the log's gains less regeneration and Spirit Refunds), damage gives 0.6 of the formula's in
    fight 4 and 1.24 in fight 30 (Emissary left out). No one factor fits both.
- **Fury** runs about 4 high in fights 4 and 31: it grows about 0.15 a second inside Thunder Call. Ride the Lightning
  capped at 30 Fury a cast, or unhasted, runs 4-10 low instead.
- **Hammer Storm** deals 1-4% more than the log beyond the other abilities' share in fights 4 and 31, at 9-12 enemies
  in fight 4 above all, the same on every spin.

## Not yet checked against a log

Left Hand Path, Pneuma, Killing in the Name and Thundering Vortex are built from their tooltips and the
game data (marked `UNVERIFIED` in their files). Blood & Thunder, The Motherload, Thunderstruck and Ace of Spades are in
q3y7pWgXrjN2dftw fight 12 alone. Sledgehammer is in fights 4 and 30 alone.
