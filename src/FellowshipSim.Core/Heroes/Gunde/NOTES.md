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
- **Reign in Blood's share counts at each hit:** Reign in Blood's and Frenzied Reign's shares hold for a hit that lands
  in Reign in Blood, whenever the cast was; Serrated Edge's holds from the cast. Rend's debuff stacks over the 20 fights
  of patch 0.5.0.0 (hits alone on their enemy in their frame, Strength unchanged): a Grim Carve spin out of Reign in
  Blood from a cast in it adds 0.49 of its damage (median, 497 spins, 16 fights: 8vb2pTAhFQRcktaD fight 2,
  PFtrX9RZJxmv1QfT fight 2, XQHxvNmwRp8TcMJG fight 3 the most), and a spin in it from a cast before it 1.05 (219 spins,
  13 fights: 67qwcRKCWYFzyxPA fight 6, VP9bnawhd6prjfKN fight 8 the most). Bloodbound Spirit's pulses alike: 1.00 out of
  it from a cast in it (66 pulses, 9 fights), 2.08 in it from a cast before it (585 pulses, 13 fights: XQHxvNmwRp8TcMJG
  fights 2 and 3 the most). Blueprint GA_Gunde_Passive_PerTargetAccumulatedBleed_Monitor (build 26320967) tests Reign
  in Blood's tag on each damage event, and Serrated Edge's on the spec's captured tags. The replay's Rend calibration
  goes from 1.009 to 1.004 with it.
- **Shares in the logs:** the same measure gives each damage ID's share, out of Reign in Blood and in it: Blood Arc
  0.50 and 1.05, Reaver's Edge 0.51 and 1.07, Grim Carve 0.53 and 1.08, Double Strike 0.47 and 0.98, Rupture 0.51 and
  1.34, Heart Splitter 1.28 and 1.83 (Serrated Edge on most casts), Bloodbound Spirit 1.00 and 2.08 (medians, each in 13
  to 20 fights). The log's Rend over the replay's on the same hits is 0.95 to 1.01 for each. Fated Strike, Attack,
  Warbound, Butcher's Hook, Emerald Judgement, Exsanguinate and Slaughter add no Rend (median 0 in every fight). The
  Monitor Blueprint adds Rend only from effects with the tag `RelevantDamageEffect`: the seven above. The export's
  `DamageToDotScaler` of 0.5 on Warbound and Butcher's Hook is a tooltip token. A Rend tick takes 0.905 of its damage out
  of the Rend (median, 27,475 ticks, Deep Rend's 1.1). A Strength change rescales every stack: a hit in the frame of a
  Rising Spirit stack can lower the stacks.
- **Ticks:** each application deals a tenth of its damage per tick for 10 ticks (Deep Rend: 11 ticks). The ticks come
  on the enemy's own timer, from its first application, every 3s or every 82 server frames (2.733s) with Deep Rend, not
  hasted. A later application keeps that timer. An application ends after its own ticks: Bloodfang#4's tick @ 139.560
  drops by exactly the three applications that ticked first.
- **Stacks:** a Rend stack is a Strength of the Rend the buckets hold, Deep Rend's extra excluded, rounded (Blueprint,
  build 26320967). Owed in Blood's large casts add fewer Rend stacks than stacks held (18 casts above 15: 1319 Rend
  stacks for 1505). Each stack saves the Strength at its pickup (Blueprint), and that model replays the 7 fights closer
  to the log than one stack each up to 15 and 0.85 beyond (exact replay 97.9% to 98.8%). An action list reads the
  stacks on the target as `debuff.rend.stack` (and `dot.rend.stack`, `active_dot.rend`): the stacks of log debuff
  1003231.
- Ticks never crit and take no damage multipliers from Gunde. They take the target's incoming-damage multipliers
  (Blueprint preset `GundeRend`); the replay's Rend calibration goes from 1.051 to 1.002 with them.

## Heart Splitter

- **Exsanguinate:** 30% of the Rend still to deal on the target, after the strike's own Rend. The Rend already dealt
  is not in it. It never crits and takes no damage multipliers, its target's included: on an enemy taking 4 times the
  damage, the strike's own Exsanguinate is 1.000 of the exact replay's (medians; 67qwcRKCWYFzyxPA fight 6, 24 hits;
  8vb2pTAhFQRcktaD fight 2, 16 hits; DYL3Ph6Fkgp8jVbw fight 1, 13 hits; 77 hits in 8 fights), and the explosion's hit on
  such an enemy equals its hit on another enemy of the pulse (Ab1yCxVcNBHPz6t8 fight 7, 77 hits). Game data (build
  26320967): effect 3239 and the explosion have the preset Empty of DT_EffectCalculationPresets.
- **Oathshatter's explosion:** each other enemy takes 50% of Exsanguinate x sqrt(10/N) beyond 10 other enemies
  (Blueprint row Gunde.HeavyMeleeDotBased.Talent.CritToAoe.TargetThresholdScaling, build 26320967). The explosion's hit
  over the strike's own Exsanguinate is 0.5000 on 9 and on 10 others, 0.4767 on 11 and 0.4082 on 15 (Ab1yCxVcNBHPz6t8
  fight 7: 99, 190, 121 and 135 hits; CPNgLQD4Htynkh2G fight 6: 153, 100, 264 and 390 hits); 15,655 of the 15,877 hits
  on 9 to 22 others in 19 of the 20 fights of patch 0.5.0.0 are within 0.002 of it. The replay's Exsanguinate
  calibration goes from 1.059 to 1.003 with it. It is logged as Exsanguinate.
- **Bleeding Hearts:** the extra strike lands 0.23-0.30s after the cast with its own Exsanguinate, and adds no cast event.

## Other abilities

- **Slaughter:** 160% of the Rend each enemy still had to deal, over 10 ticks of 0.3s from 0.3s after the cast. A
  Rend of 1264 with 2 of 11 ticks dealt became 10 ticks of 165 (Desecrator, n7zxK8harZT2wjDB fight 9). Each tick takes
  its target's damage-taken multipliers, as Rend's tick does (game data, build 26320967: the bleed
  GE_Gunde_InstantAoeDot_Dot has the preset GundeRend of DT_EffectCalculationPresets, and the damage execution reads
  the target's IncomingDamageMultiplier at each hit and tick of an effect with that preset's flag, binary 0x1450DB180
  and 0x145107B15). The log's tick over the exact
  replay's tick without the multiplier is 4.000 on an enemy taking 4 times the damage (medians; CwmX2Tb8GA6hyc1v fight
  8, 124 ticks; Ab1yCxVcNBHPz6t8 fight 7, 69 ticks; 67qwcRKCWYFzyxPA fight 6, 32 ticks; 250 ticks in 6 fights), 1.100
  and 1.300 at 1.1 and 1.3 times (DrabZB8wYcQ4fx6m fight 33, 67 and 46 ticks) and 0.996 on the other enemies (13,976
  ticks). The replay's Slaughter calibration goes from 1.066 to 0.999 with it.
- **Rend after Slaughter:** Slaughter takes every Rend stack (1,557 enemies in the 20 fights of patch 0.5.0.0: 86
  stacks each before, 0.1 after). The Rend grows back to 8.5 stacks 3s later, 16.8 at 6s, 24.2 at 10s and 29.5 at 20s.
  The exact replay's Rend on the same enemies is 0.991, 0.976, 0.988 and 0.966 of it. Blueprint GA_Gunde_InstantAoeDot
  (build 26320967) zeroes every bucket of each enemy's Rend and removes it; the next hit with a share starts a new Rend.
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

## Not yet explained

- **Rend after a Slaughter in a sim.** Azif's character on the logged run's dungeon (Ab1yCxVcNBHPz6t8 fight 7,
  Cithrel's Fall), `ActionLists/Default.simc`, 200 fights: Slaughter 1.09 times a minute against the log's 1.15,
  Exsanguinate's average hit 16,272 against the log's 18,219, Rend's tick 4,665 against 5,427, Slaughter's tick 8,138
  against 9,213; hero DPS 51,315 against 51,975; 4.9 enemies alive against 4.8. The exact replay's Rend after Slaughter
  follows the log's (see Rend after Slaughter), and the log's Grim Carve, Blood Arc and Reaver's Edge hits are 0.99 of
  the replay's. The dungeon import gives each enemy its own time, from the import player's first contact with it to
  its own death: 7,559 enemy-seconds for the run's 125 enemies, against 7,127 between Gunde's first and last event
  with each. The differences below remain between the sim's fight and the log's. Each is measured with a harness
  outside the repository on that dungeon (64 fights each, Gunde's events of the log alone; the list alone:
  Exsanguinate 16,226, Rend 4,673, Slaughter 8,072, Grim Carve 1,833, Blood Arc 2,435, Reaver's Edge 1,523, hero DPS
  51,317):
  - **Anomalous Orbs.** A Grim Carve, Blood Arc, Reaver's Edge or Bloodbound Spirit hit on an Anomalous Orb is 3.0 to
    5.5 times the hit on another enemy of its pulse in 190 of 307 pairs and 0.8 to 1.3 times in 117 (Anomalous
    Instability, effect 2711; the replay has it as 4 times the damage taken). Game data (build 26320967): affix 24's
    orb has `IncomingDamageMultiplier` MultiplyCompound 4 while it is not casting Detonate (ability 1903, a 15 s cast;
    12 s to the recast after an interrupt). Oathshatter's explosion takes none of it (77 hits on orbs, each 0.8 to 1.3
    of the explosion's hit on another enemy). The orbs take 8.8% of Gunde's damage in the log, 20% of Slaughter's and
    10% of Rend's. A sim's orbs take no multiplier. With 4 times the damage for 62% of each orb's time: Rend 5,299,
    Slaughter 9,179, Grim Carve 2,073 against the log's 2,216, Blood Arc 2,757 against 2,892, Reaver's Edge 1,748
    against 1,801, hero DPS 54,511.
  - **Storm's Fury.** The Storm Shield affix's Storm's Fury (affix 25, ability 1946: a bolt every 59 s to 72 s; effect
    2699, 20% crit chance for 20 s) is on Gunde at 24% of his Attacks in the log and at none in a sim: crit chance
    42.3% in the exact replay against 36.9%. With one Storm's Fury every 80 s added: Exsanguinate 16,720, Rend 5,510,
    Slaughter 9,565, Grim Carve 2,168, Blood Arc 2,906, Reaver's Edge 1,809, Heart Splitter 8,657 against 8,484. Grim
    Carve, Blood Arc, Reaver's Edge, Heart Splitter and Attack crit within 2 points of the log's.
  - **Reach.** An area pulse in the log hits nearly every enemy in combat with Gunde: Grim Carve 4.59 of 5.05, Blood Arc
    4.87 of 4.96, Reaver's Edge 4.58 of 4.76, Oathshatter's explosion 5.00 of 5.04, Slaughter 3.73 of 3.83 enemies with
    Rend. A sim's area pulse hits every enemy in combat. Game data (Blueprints and binary, build 26320967): a cylinder
    of radius 700 around Gunde for Slaughter and Reaver's Edge, a pie of radius 700 and 120 degrees along Gunde's
    facing for Blood Arc, a cylinder of radius 700 around Heart Splitter's target for Oathshatter; each reach adds the
    target's hit-volume radius, and none tests line of sight. The log's hits agree: Slaughter to 678 units with its 3
    misses at 708 or more; Reaver's Edge 1,103 of 1,160 within 700 and the rest to 878, the farthest on a boss. Grim
    Carve's area is a cylinder of radius 340 around its target: the row's 850 is applied as a scale of 0.02 on a
    cylinder component of radius 20 (binary 0x1452BA7A0 and 0x1452351D0; the Blueprint sets no other size). The log's
    hits are within 484 units of the cast's target (2,128 of 2,189 within 400) and 93 enemies in combat between 500 and
    900 units of it are not hit. Bloodbound Spirit's cylinder has radius 1000 around Gunde (SetCylinderSize, binary
    0x1452BE4A0). With the log's miss rates added: Grim Carve 2,207, Blood Arc 2,902, Reaver's Edge 1,822, Exsanguinate
    16,595, Rend 5,377, Slaughter 9,319.
  - **The target in a boss fight.** With a boss in combat, Azif's Heart Splitter strikes the boss at 232 of 232
    strikes. The list's first action, `switch_target,target_if=target.is_boss`, sets a boss in combat as the target,
    and the sim's Heart Splitter strikes the boss at 235.5 of 235.6 strikes a fight. Rend on a boss ticks for 12,086
    against the log's 14,636, Exsanguinate on a boss deals 27,950 against 33,455, and Slaughter's ticks on a boss are
    165 a fight at 25,085 against 150 at 21,127. Without that action the sim's target is an enemy of the run's first
    pull, then the first enemy in combat as its target dies: 176.7 of 235.7 strikes on the boss, Rend on a boss 9,710
    and Exsanguinate on a boss 24,437.
  - **Slaughter's casts.** The list's Bleeding Hearts Slaughter action has the conditions of Azif's 30 casts: Grim
    Carve on cooldown at 29; on a boss, 200 Rend stacks or more on the target at 12 of the 16 (log debuff 1003231 on
    the last Heart Splitter's target, median 228), each of the 16 in Open Wounds, 8 in its last 6 s and 9 right after
    a Heart Splitter; on trash, 170 Rend stacks or more at 10 of the 14 (median 208), 12 of the 14 with the target at
    20% health or less (the others at 20.0% and 25.7%) and each with no Heart Splitter charge. The sim's casts against
    the log's, read from the engine's state in the sim and in the exact replay: 28.6 a fight against 30; 44.8 s
    between casts at the median against 44.8; the cooldown ready for 21.9 s at the median against 25.2; 83% right
    after a Heart Splitter against 67%; 94% in Open Wounds against 83% and 41% in its last 4 s against 40%; 16.6 on a
    boss and 12.0 on trash against 16 and 14; the trash target's health at the median 17% against 10%; 4.37 enemies
    in combat against 4.00; 255 Rend stacks on the target at the median against 223, and 268 against 224 on a boss.
    Without the stack check on trash the action casts 32.7 times a fight on this dungeon, 15.8 of them on trash.
  With Anomalous Orbs, Storm's Fury and reach added: Grim Carve 2,207, Blood Arc 2,902 and Reaver's Edge 1,822 are
  within 1.2% of the log's, Rend 5,377 is 0.9% below, Slaughter 9,319 is 1.2% above and Exsanguinate 16,595 is 8.9%
  below (the explosion 13,800 against 14,875, the strike's own 27,200 against 30,431); hero DPS 55,200 against the
  log's 52,523 over the same seconds. The test's orb multiplier is on at times of its own: Rend on an orb ticks for
  12,313 against the log's 8,986 and Slaughter for 24,558 against 20,967. Rend on a boss ticks for 12,354 against
  14,636, and Slaughter's ticks on a boss are 170 a fight at 25,632 against 150 at 21,127: the Rend ticked and
  consumed on a boss is 6.0 million a fight against the log's 5.8 million (Slaughter's damage over 1.6), and the
  sim's Slaughter takes 2.7 million of it against 2.0 million. The sim casts more than Azif: Heart Splitter 14.45
  times a minute against 13.30, Blood Arc 16.63 against 15.04, Reaver's Edge 12.27 against 9.73, Double Strike 11.15
  against 6.23. Sources checked: Rend's debuff stacks in the 20 fights; the export's Slaughter record (radius 700,
  maxTargets 99999); the Blueprints GA_Gunde_InstantAoeDot (every enemy within 700 that is alive, in combat and holds
  Rend; all of its Rend zeroed and removed), the Rend DotManager, GA_Gunde_Util_ApplyAccumulatedDotToTarget and the
  Monitor (no graph gives Rend back after a Slaughter; Slaughter's ticks add none);
  `RandomOutcomeSource.SelectTargets`; the fight's `dungeonstart` affixes (4, 6, 24, 25, 22).
- **Owed in Blood's Rend.** The log's Rend rise at a cast is 0.979 of the engine's (median, 292 casts with Strength
  unchanged in the 20 fights; quartiles 0.931 and 1.006; per fight 0.91 to 1.04). Sources checked: Rend's debuff
  stacks; the export; the Blueprints BP_Gunde_DotTransfer_Orb, GA_Gunde_Passive_DotTransfer_OrbDropper and
  GA_Gunde_DotTransfer, the self-buff's defaults and the constants rows (each pickup saves its stacks times Gunde's
  current Strength, capped at 150 times it; the cast adds the saved damage with no scaler: the engine's model).

## Known gaps

- Free dice gives Slaughter the enemies Gunde hits between its cast and the next cast. In Ab1yCxVcNBHPz6t8 fight 7 that
  is 77 of the 112 enemies Slaughter hit: 10 of the 30 casts have another cast within 0.3s, before Slaughter's first
  tick. Free dice's Slaughter is 52.5% of the log's damage and its Rend 122.8% (20 fights).
- Over the 20 fights of patch 0.5.0.0 the engine's hit is above the log's for Grim Carve (log over engine 0.932),
  Heart Splitter (0.984, crits 0.967), Bloodbound Spirit (0.978) and Attack's normal hits (0.885), and below it for
  Fated Strike (1.050). Every other damage ID over 1% of the damage is within 1%.
- A training dummy has 100,000,000 health. In a fight whose health does not follow time, Bleeding Hearts' crit chance
  above 80% health holds all fight, and Darkening Hearts' at 30% health or less never does.
