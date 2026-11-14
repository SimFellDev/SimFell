# Gunde: what the logs showed

The mechanics below are from the combat logs, beyond the tooltips. Each point is also in a comment beside its code. Measured on 5 Gunde logs (Apostate Veras, Urrak Markets, Empyrean Sands and Wyrmheart, July to September 2026)
unless noted. Only the two Y4wpb2f3gc8LqJDX fights (22 September 2026) are from after patch 0.4.5.0.

## Rend

- **Applications:** each hit that applies Rend adds its damage dealt times its share: 50% (100% for Heart Splitter),
  plus Reign in Blood's 30% and Frenzied Reign's 25%, plus Serrated Edge's 20% and Superior Serration's 10%. Heart
  Splitter in Reign in Blood with Serrated Edge adds 1.85 of its damage (n7zxK8harZT2wjDB fight 9 @ 16.402).
  Bloodbound Spirit's whole share, Reign in Blood's and Frenzied Reign's included, is doubled (Blueprint, build
  26320967): 100% alone and 160% in Reign in Blood. The replay of 7 fights comes closer to the log with it (exact
  replay 97.5% to 97.9%).
- **Ticks:** each application deals a tenth of its damage per tick for 10 ticks (Deep Rend: 11 ticks). The ticks come
  on the enemy's own timer, from its first application, every 3s or every 82 server frames (2.733s) with Deep Rend, not
  hasted. A later application keeps that timer. An application ends after its own ticks: Bloodfang#4's tick @ 139.560
  drops by exactly the three applications that ticked first.
- **Stacks:** a Rend stack is a Strength of the Rend the buckets hold, Deep Rend's extra excluded, rounded (Blueprint,
  build 26320967). Owed in Blood's large casts add fewer Rend stacks than stacks held (18 casts above 15: 1319 Rend
  stacks for 1505). Each stack saves the Strength at its pickup (Blueprint), and that model replays the 7 fights closer
  to the log than one stack each up to 15 and 0.85 beyond (exact replay 97.9% to 98.8%).
- Ticks never crit and take no damage multipliers from Gunde. They take the target's incoming-damage multipliers
  (Blueprint preset `GundeRend`); the replay's Rend calibration goes from 1.051 to 1.002 with them.

## Heart Splitter

- **Exsanguinate:** 30% of the Rend still to deal on the target, after the strike's own Rend. The Rend already dealt
  is not in it. It never crits and takes no damage multipliers.
- **Oathshatter's explosion:** each other enemy takes 50% of Exsanguinate x sqrt(8/N) beyond 8 other enemies (the
  tooltip says 10): 1640 on 15 others of 4492, and 0.2773 of it on 26 others in four casts. It is logged as Exsanguinate.
- **Bleeding Hearts:** the extra strike lands 0.23-0.30s after the cast with its own Exsanguinate, and adds no cast event.

## Other abilities

- **Slaughter:** 160% of the Rend each enemy still had to deal, over 10 ticks of 0.3s from 0.3s after the cast. A
  Rend of 1264 with 2 of 11 ticks dealt became 10 ticks of 165 (Desecrator, n7zxK8harZT2wjDB fight 9).
- **Grim Carve:** spins land 0.17-0.24s, 0.64-0.71s and 1.14-1.21s after the cast, not hasted. Serrated Edge is used up
  as Grim Carve is cast, and every spin gets it.
- **Crimson Strikes:** the next cast that applies Rend gets +15% on all its hits. A Grim Carve spin after a Blood Arc uses it up without the bonus if the Grim Carve was cast first. Bleeding Hearts' extra strike has no share of it.
- **Serrated Edge:** lasts 8s (the tooltip says 10). The next Heart Splitter, Rupture, Reaver's Edge or Grim Carve uses it
  up; Owed in Blood, Reign in Blood, Slaughter, Fated Strike, Warbound and Jawbreaker leave it.
- **Bloodbound Spirit:** 5 pulses, 0.035s after the cast and then every 1.5s, each with the damage of the first (Drakheim's Absolution's Main Stat from the cast included). It has no cast time since patch 0.4.5.0.
- **Fated Strike:** the cleave hits are logged as Fated Strike too; each is 2.874 / 7.39 x sqrt(3/N) of the hit on the
  target.
- **Attack:** a 1.5s swing shortened by Haste (1.27-1.30s apart at about 15% Haste).

## Resources

- Gunde has no resource of his own: the logs report none for him.
- **Spirit Refund:** Rising Spirit comes as the first hit of an ability that applies Rend lands, and 5 Blood Feathers
  come with it; their Owed in Blood stacks follow about 0.5s later (Y4wpb2f3gc8LqJDX fight 25 @ 24.978).
- **Owed in Blood:** stacks last 90s (45s before patch 0.4.5.0). A pickup at 150 stacks is logged as a refresh with no
  stacks. A replay follows the log's stacks; a sim picks up every feather 0.5s after it appears.

## Legendaries

- **Carrion Onslaught** (Northlander's Wraps, item 5900; cnjfpzmD7ZXakMFH fight 33). The datamine's item names talent 765
  and no power; the logs show Carrion Onslaught. Owed in Blood's cast grants effect 3256 (one stack) as it consumes its
  stacks. The next Blood Arc deals 1.2 + 0.02 per stack consumed times its damage (the replay's Blood Arc damage: 1.006
  of the log's with it, 0.866 without), and swaps 3256 for effect 3257 with a stack per Blood Feather to drop, a third
  of the stacks consumed (84 -> 28, 51 -> 17, 80 -> 27). It drops 10 at once and 10 more every 2s.

## Not yet checked against a log

- Ancestral Instinct, Murder of Crows, Raven's Precision, Bloodcraze, Sundered Flesh and Massacre are built from their
  tooltips and the game data (marked `UNVERIFIED` in their files).
- The chance of a Blood Feather per Rend tick has no game data; 0.15 is an estimate from fight 25.
- Bloody Mess's extra spins come as far apart as the first three.

## Known gaps

- Gunde's direct hits come out 3-8% above the logs by median (Fated Strike matches); Rend, Exsanguinate and Slaughter
  have the same gap. The gap differs between two fights of one character on one day, so its cause depends on the fight.
- A sim decides crits by expected value, so every Heart Splitter at more than 50% crit chance explodes through
  Oathshatter, and none below. Sim targets have 100,000,000 health: Bleeding Hearts' crit chance above 80% health holds
  all fight, and Darkening Hearts' at 30% health or less never does.
