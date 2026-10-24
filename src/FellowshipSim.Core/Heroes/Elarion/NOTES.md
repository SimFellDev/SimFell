# Elarion: what the logs showed

How Elarion's mechanics were pinned down, beyond the tooltips. Each point is also a comment next to the code it shapes.
Measured on 2 Elarion logs of 2026-10-23 (fXyzwGcQkTrLD34R fight 4 Stormwatch, pWmcV42K39GHTLQX fight 23 Wraithtide
Vault) unless noted. Game values are from game data build 26136736.

- **Fight 4** ([OLK] Wulf): Focused Expanse, Resurgent Winds, Lethal Shots, Fervent Supremacy, Final Crescendo, Swift
  Reload, Skylit Grace; Astronomer's Hail.
- **Fight 23** (ruru): Piercing Seekers, Fusillade, Repeating Stars, Lunar Fury, Rising Moon, Lunarlight Affinity,
  Fervent Supremacy, Last Lights, Skylit Grace; Starstriker's Ascent.
- Both players wear Asha's Chronoshift Bow (Chronoshift).

## Damage

- **Class tuning:** +8% damage (hero attribute `BalancingDamageAura` 1.08).
- **Damage IDs:** Lunarlight Salvo is 1001877 and its eruption 1002832. Highwind Arrow's ricochets, Heartseeker
  Barrage's Piercing Seekers ricochets and every Multishot, Empowered or not, are logged under the ability's own ID.
- **Lunarlight Salvo:** 23-34% of the hits on a marked enemy that do not crit call down a salvo, and 46-57% of the
  crits (4,984 salvos). Starfall Volley, Multishot, Highwind Arrow, Heartseeker Barrage, Celestial Shot, Focused Shot,
  Chronoshift and the augmentations' hits (Amethyst Splinters, Emerald Judgement, Ruby Storm) call them down;
  Shoot does not (5 of 603 hits on a marked enemy, in the frame of another hit). The salvo is logged within 2ms of its
  hit, on the same enemy.
- **Lunar Fury:** 50% of Heartseeker Barrage's hits on a marked enemy that do not crit call down a salvo, and every crit.
- **Eruption:** a salvo Heartseeker Barrage called down erupts 25% of the time, in its server frame, on the enemies near
  the salvo's enemy and not on that enemy.
- **Highwind Arrow:** each ricochet deals half the first hit, with the cast's bonuses and crit chance (a Lethal Shots
  cast's ricochets are grievous crits too).
- **Piercing Seekers:** each arrow's ricochet deals 70% of the arrow and lands about 0.07s after it.
- **Starfall Volley:** each hit deals the same damage on every enemy. With Astronomer's Hail the volley's own target
  takes twice what the other enemies take.
- **Multishot:** one arrow on each enemy around the target. An Empowered Multishot strikes 3 enemies where a plain one
  strikes 1 or 2.

## Timing

- **Highwind Arrow:** the first hit lands 0.13s after the cast. Each ricochet strikes another enemy 2 to 6 server
  frames after the hit before it.
- **Heartseeker Barrage:** an arrow every 0.2s shortened by Haste; the first lands about 0.25s after the cast. The part
  of a tick left as the channel ends looses one more arrow of full damage (fight 23 @ 81.335: 22 arrows on the target
  in a 2.5s channel at 73% Haste).
- **Starfall Volley:** the arrows land on each enemy at its own moments, 0.5-0.8s apart at the median for one enemy, the
  first 0.2-0.4s after the cast. The engine hits every enemy as the volley is cast and every hasted second after: it
  has 13-20% more hits than the logs (enemies outside the volley's area).
- **Multishot:** the arrows of one cast land over up to 0.1s.
- **Shoot:** shots at least 1.1s apart, most 1.3-1.8s, never while casting or channeling.

## Resources

- **Focus:** resource type 2, in hundredths; 100 at most, full at the start. It regenerates at 5 per second raised by
  Haste (fight 23: within 1 Focus of the log over the first 40s). Focused Shot's 20 Focus comes as the cast completes.
- **Spirit Refund:** gives back the Focus spent, adds 5 stacks of Lunarlight Mark to the target and 2 to up to two
  nearby enemies, and 1 Spirit Point. With two spends in one frame (fight 23 @ 39.10: Highwind Arrow, then an Empowered
  Multishot) the refund is the later spend's (10 Focus); the replay gives it to the first, so Focus runs 20 high until
  it caps.

## Buffs and debuffs

- **Lunarlight Mark:** every application adds its stacks to the ones held and restarts the 15 seconds. Each salvo takes
  a stack, but for a salvo from a Starfall Volley arrow with Lunarlight Affinity.
- **Celestial Impetus:** comes as Focused Shot's arrow lands (43 of 78 Focused Shots in fight 23, 44.3 expected at 2.0
  procs per minute).
- **Focused Expanse:** the charge comes as Focused Shot's cast completes, before the arrow lands.
- **Fervent Supremacy:** the cast applies Skystrider's Supremacy at 4 stacks; each Multishot takes one.
- **Final Crescendo:** the first and second Highwind Arrows add a stack; the third uses both.
- **Impending Heartseeker:** Starstriker's Ascent's buff from a barrage's own Spirit Refund is left for the next barrage
  (fight 23 @ 15.192). Its arrows rise from about 1.05 to 2.8 times a plain arrow over 18 arrows.
- **Skylit Grace:** effect 2316 is not in the logs; the engine's aura is internal.
- **Resurgent Winds:** Lunarlight Mark always grants it.

## Replay matches

| Fight | Hits matched | Damage of log |
|---|---|---|
| fXyzwGcQkTrLD34R fight 4 | 3605/3734 | 102.4% |
| pWmcV42K39GHTLQX fight 23 | 30583/31837 | 95.9% |

Fight 4 runs high on Starfall Volley's hits on enemies outside its area. Fight 23 runs low on 421 salvos the engine
does not call down, most from hits the engine lands at other moments (Amethyst Splinters, Heartseeker Barrage,
Starfall Volley).

## Not yet checked against a log

Deadly Focus, Skyward Munitions, Striker's Aim, the Impending Heartseeker talent and Shimmer are built from their
tooltips and the game data (marked `UNVERIFIED` in their files). Which casts count as Resurgent Winds' Offensive
Abilities, and the first Impending Heartseeker arrow's bonus, are also `UNVERIFIED`.
