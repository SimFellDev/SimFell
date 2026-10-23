# Tariq: what the logs showed

How Tariq's mechanics were pinned down, beyond the tooltips. Each point is also a comment next to the code it shapes.
Measured on 3 Tariq logs of 2026-10-23 (fXyzwGcQkTrLD34R fight 4 Stormwatch, 1p7AXmHfwV9CYMxR fight 30 and
YC861NTRgLbmw4Qa fight 31 Cithrel's Fall) unless noted. Game values are from game data build 26136736.

## Damage

- **Damage IDs:** Skull Crusher's thunder bolt is logged under 1229 and Hammer Storm's lightning under 1242, as their
  physical hits are. Heavy Strike is three effects: 1776 on the target, 2755 on the other enemies, 1779 for the
  lightning on all of them. Face Breaker's splash is 2241, Sledgehammer 2061, Chain Lightning 1774, Raging Currents
  1767, Raging Tempest's leap 1231 and Leap Smash 1225.
- **Hammer Storm:** 3 spins, the first one server frame after the cast and then every hasted half second. Each spin
  deals 1.35 times the one before. Every enemy's physical hit comes first, then every enemy's lightning.
- **Face Breaker's splash:** exactly 50% of the hit dealt to the target, crit included. It never crits and loses damage
  as sqrt(12/N) beyond 12 enemies.
- **Sledgehammer:** exactly 10% of the physical Skull Crusher hit dealt (the talent's text; ability 2061 says 30%). It
  never crits.
- **Culling Strike:** 40% more damage per Fury spent, up to 10 Fury. Executioner's Grin counts as 30 Fury spent (the
  legendary's text; the effect's says 20).
- **Leap Smash:** +40% crit chance of its own (placeholder constant `Talent.IncreasedCriticalChance.AddedCrit`). It
  crits 72-86% of the time against 35-44% for Face Breaker.
- **Them Bones:** grievous Skull Crusher crits come with the thunder bolt as well as the physical hit.
- **Wild Swing:** never crits.

## Timing

- **Skull Crusher's global cooldown:** 1s before haste (`GE_GlobalCooldown_TariqHeavySingleTarget`). The next cast
  comes 2/3 of the usual global cooldown after it.
- **Off the global cooldown:** Culling Strike (its ability has no global cooldown effect; casts come 5ms after others),
  and the abilities whose tooltip says so.
- **Hasted cooldowns:** Skull Crusher, Hammer Storm and Wild Swing, as well as the ones the game data lists. Skull
  Crushers come 0.835s apart at 20% haste.
- **Leap Smash** lands 0.27-0.30s after the cast; Mouth for War's Focused Wrath stack and High Road's Fury come with
  it. **Raging Tempest's leap** lands 0.30-0.46s after the cast.
- **Chain Lightning:** the first hit 0.30s after the cast, then a hit every 0.05s. It hits enemies it has not hit
  yet first.
- **Raging Tempest:** the buff comes with the cast at 1 stack and lasts 20s from the cast. Each Raging Currents hit adds
  a stack and leaves the time left as it is. Raging Currents strikes every hasted second from the cast. Raging Tempest
  removes Thunder Call as it starts.

## Resources

- **Fury** is logged as resource type 2, a share of a maximum that follows Strength. The replay reads it as a share of
  100. It carries over between pulls.
- **Bloodline** and **Ride the Lightning** generate Fury on hasted ticks.
- **Executioner's Grin** rolls on every cast, the weapon's too (fight 4 @ 66.7: Fated Strike).

## Replay matches

| Fight | Hits matched | Damage of log |
|---|---|---|
| fXyzwGcQkTrLD34R fight 4 | 5464/5676 | 95.8% |
| 1p7AXmHfwV9CYMxR fight 30 | 6164/6438 | 91.6% |
| YC861NTRgLbmw4Qa fight 31 | 13507/13698 | 90.0% |

In fights 30 and 31 the set bonus Drakheim's Absolution (effect 3269, +20% Strength for 20s from the Spirit ability) is
in the log, and the new logs' gear has no set data to import it from: every hit in a Raging Tempest window reads about
1.2 times the engine's.

## Not yet checked against a log

Left Hand Path, Blood & Thunder, The Motherload, Thunderstruck, Pneuma, Kill Em All, Ace of Spades, Killing in the Name
and Thundering Vortex are built from their tooltips and the game data (marked `UNVERIFIED` in their files). Crack the
Sky is in fight 31 alone, Sledgehammer in fights 4 and 30 alone.
