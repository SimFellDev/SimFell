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
- **Them Bones:** grievous Skull Crusher crits come with the thunder bolt as well as the physical hit.
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
- **Bloodline** and **Ride the Lightning** generate Fury on hasted ticks, Bloodline in combat alone (1p7AXmHfwV9CYMxR
  fight 30: no Fury between pulls). Ride the Lightning's Fury comes in Raging
  Tempest too (fight 31 @ 199.7: about 2.2 Fury a second, with Thunder Call gone).
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

## Not yet checked against a log

Left Hand Path, Blood & Thunder, The Motherload, Thunderstruck, Pneuma, Kill Em All, Ace of Spades, Killing in the Name
and Thundering Vortex are built from their tooltips and the game data (marked `UNVERIFIED` in their files). Crack the
Sky is in fight 31 alone, Sledgehammer in fights 4 and 30 alone.
