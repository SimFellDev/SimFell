# Mara: what the logs showed

How Mara's mechanics were pinned down, beyond the tooltips. Each point is also a comment next to the code it shapes.
Measured on 3 Mara logs (Fc8v6yWxD14GQMnV fight 19, dBgbn8wD1RxL7Kma fight 9, pWmcV42K39GHTLQX fight 23, October
2026) unless noted. All three players took Sinner's Pride, Assassin's Guile, Feed the Queen, Malevolence, Deadly Scheme,
Venomous Delight and Macabre Stratagem, and wore Sahril's Wrath.

## Damage

- **Class tuning:** +4% damage (hero attribute `BalancingDamageAura` 1.04).
- **Target falloff** (damage per target beyond T targets is sqrt(T/N)): Arachnid Assault 8, Skittering Blades 8,
  Sahril's Wrath 1.
- **Shadowy clones** (Matriarch Macabre, Macabre Stratagem): each clone repeats the attack at 50% of the cast's damage,
  with the cast's bonuses (Malevolence, Assassin's Guile, Deadly Scheme's crit) but not Feed the Queen (clone Queen's
  Fangs came in 0.71x the engine's with it). Their hits are logged under the attack's own ID.
  A clone's Arachnid Assault has the cast's target falloff and strikes the enemies the cast struck. In the 23 Mara fights of
  `data/fights`, a clone's hit on an enemy over the cast's hit on that enemy, crits removed, has a median of 0.49 to 0.51 at
  every target count from 2 to 26 (858 casts, 11,351 hits at 9 enemies or more), and 14,227 of 14,239 clone hits over 1,104
  casts are on the cast's enemies. T2qLPpmMkrAtJ9B1 fight 18 @ 349.933: the cast strikes 6 enemies, and each clone strikes
  those 6 alone. A clone's Arachnid Assault keeps Mara's numbers at the cast as a Queen's Fang's does. In the game data (build
  26320967), `GA_Mara_AoeAttackSpender` offset 1415 writes SetByCaller.DamageScaleTargetCount from the cast's target count, and
  offset 1883 passes that spec and the cast's target data to `GA_Mara_Passive_CopySpawner`, with no target query of its own:
  each clone shuffles that list (offset 79), and `GE_Mara_AoeAttackSpender_Damage` applies the falloff to the cast and both
  clones.
  Both clones of a Queen's Fang keep Mara's numbers at the cast, with Maiden of Death. Over the 2,580 clone hits in the 23
  Mara fights of `data/fights`, read at the strike: the 1,715 with no player aura change since the cast are 1.0023x the
  engine's, the 77 after Matriarch Macabre ended 1.36x, the 56 after Maiden of Death ended 1.27x, the 46 after Maiden of Death
  came 0.79x and the 39 after The Philosopher came 0.79x. T2qLPpmMkrAtJ9B1 fight 18 @ 1249.421: Matriarch Macabre and
  Drakheim's Absolution end at 1250.056 between clone hits of 26,814 and 26,400. pWmcV42K39GHTLQX fight 23 @ 276.428: The
  Philosopher and Maiden of Death come at 276.592, before clone hits of 29,035 and 30,143 (0.76x and 0.79x read at the strike).
  In the game data (build 26320967), a clone applies a copy of the cast's spec: `GA_Mara_Passive_CopySpawner` offset 4412
  `CloneSpecHandle` (0x144A62970 keeps captures and SetByCaller values) and offset 2688, with `SetEffectContext` at offset 4600
  re-snapshotting in the cast's frame (0x144A6F7D0). `CRAgilityDamageCalculation` (0x1450D73F0) has source attributes as
  snapshots and target attributes read at the strike, and each application rolls its own crit (`Execute_Implementation`
  0x1450DCF40, roll 0x14510C4D0).
- **Volatile Poison:** each tick deals the 31.2% of Agility the tooltip gives for the whole poison; the eruption deals
  124.8%.
- **Caustic Poison:** every hit is a grievous crit (its +100% crit chance on top of Mara's).
- **Vexira's Venom:** a tick deals a third of the venom held; the shadowy clones' crits add none.
- **Blocked hits** (hit type 4; 40 in fight 19, Queen's Fang and auto attacks): the logged amount is what the block
  left, so a replay compares the engine's hit with the amount plus the blocked part.

## Timing

- **Global cooldown:** 1s, not hasted (casts 1.032s apart at the least, during Spirit of Heroism too).
- **Auto attacks:** a 1.5s swing shortened by Haste (1.30s apart at 13% Haste, 1.035s during Spirit of Heroism); none
  in Stealth.
- **Stealth** (Brooding Shadows): ends with the next Backstab, Widow's Bite or Skittering Blades, 0.67s after it starts
  at the median; Kick and Stalker Step keep it.
- **Kick:** a 16s cooldown Haste leaves unchanged, usable during the Global Cooldown, with no Energy cost, over 416 Kicks
  in the 23 Mara fights of `data/fights`: 16.062s apart at the least, 384 under 1s after another cast and 390 under 1s
  before the next. The 53 Kicks with no interrupt trigger the cooldown too (WcTBrQNX46m9axqP fight 12 @ 462.034 and
  478.331: the next Kick 16.297s and 16.062s later).
- **Shadow Ward:** usable during the Global Cooldown, with no Energy cost, and it keeps Stealth (4 casts in Stealth, each
  keeping it). Haste leaves its cooldown unchanged. None of the 298 casts in the 23 Mara fights of `data/fights` is on the
  engine's cooldown, 30s shortened by Emerald's Blessing of the Commander and Ultimatum and sped up by Strand of Eternity:
  the shortest gap, 21.309s (GXFz3pZ94aQr2chV fight 3 @ 645.307, Prvc4XmbdHBaVRKy fight 12 @ 138.322), sits on 30s x 0.78 /
  1.1 = 21.27s. It lasts 4s, 8s with The Tower: the 6 fights with The Tower have it up 8.00s to 8.04s and the 17 without it
  4.00s to 4.05s. The applybuff has duration 4000 in both, since The Tower raises the duration after it is applied.
  A cast rolls Hidden Power: 4 gains fall on a Shadow Ward cast with no other cast of Mara's within 60ms (Fc8v6yWxD14GQMnV
  fight 19 @ 571.657, p8VJDcBdxa7ghqG3 fight 124 @ 1041.55 and 1277.118, T2qLPpmMkrAtJ9B1 fight 18 @ 1290.901).
- **Web Crack:** usable during the Global Cooldown, with no Energy cost and no damage, over 204 casts in the 23 Mara fights
  of `data/fights`: 192 under 1s after another cast and 184 under 1s before the next. It keeps Stealth (2 casts in Stealth,
  each keeping it), and Haste leaves its cooldown unchanged. Its 24s cooldown has the shortest gaps 17.066s
  (v8rhNxVc3PwDqzBA fight 4 @ 440.448) and 17.235s (GXFz3pZ94aQr2chV fight 3 @ 156.206), on 24s x 0.78 / 1.1 = 17.02s. The
  stun lasts 4s, 4.011s at the median of 202, and 18 of the 20 under 3.9s end as the enemy dies. A cast rolls Hidden Power:
  4 gains fall on a Web Crack with no other cast of Mara's within 60ms (pWmcV42K39GHTLQX fight 23 @ 586.597 and 876.178,
  T2qLPpmMkrAtJ9B1 fight 18 @ 147.822 and 813.535). No enemy begins a cast under a stun, over 202 stuns.
- **Web Crack on an enemy's cast:** the stun's effect 136 runs `CRCancelAbilitiesBlockedByThisEffectsParentExecution`, which
  calls `CancelAbility` on an active ability with `AbilityType.Skill`, without `AbilityLogic.AtomicExecution`, and with an
  `ActivationBlockedTag` the stun grants (exe 0x1451085F0 -> 0x1452D21A0). This path has no interrupt and no lockout. Of 16
  enemy casts in progress at a stun, in 10 fights, none has an interrupt event and 14 have no finish, with no death
  (T2qLPpmMkrAtJ9B1 fight 18 @ 147.823, pWmcV42K39GHTLQX fight 23 @ 334.810). Abilities 2039, 56 and 734 pass the test, and 56
  has `Interruptible` false. The other two are enemy ability 2315, Blessing of the Bear (0.037s and 0.172s later,
  WcTBrQNX46m9axqP fight 7 @ 166.835 and 547.561): `GA_SJ_DruidSpiritualist_InstantAoEBuff` sets no `AbilityTags` and no
  `ActivationBlockedTags`, so the test leaves it running. The export has neither field. The engine leaves an enemy's cast
  running under the stun.
- **Stalker Step:** usable during the Global Cooldown, with no Energy cost, over 248 casts in the 23 Mara fights of
  `data/fights`: 119 under 1s after another cast and 211 under 1s before the next. It keeps Stealth (30 casts in Stealth, each
  keeping it; the Stealth removal 0.1s after gd6ByNrf81jc9aWw fight 3 @ 155.090 is the Skittering Blades). Its 2 charges fit a
  15s recharge: the tightest three casts span 10.687s (GXFz3pZ94aQr2chV fight 3 @ 580.585) and 10.691s (@ 641.600), on 15s x
  0.78 / 1.1 = 10.64s, and every cast of the 23 fights fits a recharge of 10.727s. The buff lasts 4s, 4.002s to 4.038s over 237,
  and a cast inside it refreshes it (10 casts). A cast rolls Hidden Power: 8 gains fall on a Stalker Step with no other cast of
  Mara's within 60ms (mThk4v1f8WGbBd7Q fight 1 @ 1021.108 and 1558.636, T2qLPpmMkrAtJ9B1 fight 18 @ 537.825). Every cast has no
  target in the log. `GA_Mara_ChargedTeleportBehindTarget` is `CCP_Manual`: the commit at ubergraph offset 29 raises the commit
  event, the cooldown commit raises none (exe 0x1449DEE10), so a cast has one Hidden Power roll. The cooldown commit and the
  buff follow a successful teleport, and a failed teleport spends no charge. Charges recharge one at a time, with the duration
  fixed at the first spend from full charges (`CRChargeEffect` exe 0x1450E71E0, 0x144A6FC50). `CooldownState` has the same rule:
  `Spend` sets the recharge from full charges alone (`Domain/Hero.cs:843`), and each return reuses it (`Domain/Hero.cs:911`).
  Final Stratagem's reset removes every stack, so both charges return (exe 0x1452D62C0). The 0.4s `Hero.AbilitySpamProtectionCooldown.Default.Duration` row sits
  under the shortest gap, 0.734s (zgxBpVKw2m71cDdk fight 5 @ 353.947). None of the 23 fights has Nightstalker (effect 2189),
  and no talent class of the engine grants it.
- **Hemorrhage:** ticks every 3s shortened by Haste, with a partial tick as it runs out. A refresh keeps the tick timing,
  and up to 30% of the new duration of the time left carries over.
- **Seething Poison:** ticks every 2s shortened by Haste; its 60s are not hasted.
- **Volatile Poison:** lasts between 6 and 8 seconds (effect 1731 DurationMagnitude 6 to 8) and ticks 6 times raised
  by Haste, rounded (7 at 13% Haste, 9 during Spirit of Heroism), evenly spread; it erupts 0.2s after its last tick.
- **Shadowy clones:** the first strikes 0.335s after the cast, the second 0.335s after the first is done; a clone's
  Arachnid Assault hits one enemy per server frame, in an order of its own (329 of 1,017 clone pairs in the same order).
- **Vexira's Venom:** ticks every 2s shortened by Haste, with a partial tick as it runs out; a new crit restarts its 6
  seconds and keeps the tick timing.

## Resources

- **Energy:** 200 at most, full at the start. It regenerates at about 15 per second raised by Haste (17.3 per second with no
  buff at 13% Haste), times 1.2 with Predator's Rush and 1.2 with Maiden of Death.
- **Spirit Refund:** a finisher's refund gives back its Combo Points and its Energy (fight 19 @ 25.691: 6 Combo Points
  and 40 Energy), and 1 Spirit Point.
- **Deadly Scheme:** a stack per 5 Energy spent; the stacks past 40 carry over, and while it is active the stacks keep
  counting without the buff. The log shows 1 stack as the buff comes back, until the next stack.
- **Venomous Delight:** a proc is a 10 Energy jump right after a poison hit (172 jumps of 8 to 12 in fight 19). A proc
  at full Energy shows nothing.
- **Malevolence:** a finisher uses one stack of its own buff, then grants the other.

## Not yet explained

- **Blocked Queen's Fang hits:** the 59 hits of type 4 in the 23 Mara fights of `data/fights`, all without a crit flag, are
  0.82x the engine's normal hit with the blocked part added back (41 cast hits 0.823, 18 clone hits 0.827). The blocked part is
  81% to 83% of that amount in each one. Sources checked: the raw `blocked` field and `LogDamage.UnblockedAmount`.
- **Web Crack with no stun:** 2 of 204 casts have no debuff (p8VJDcBdxa7ghqG3 fight 124 @ 448.011, w7khnMKAa4TxXrmC fight 23
  @ 337.767), and 2 stuns end early with no death (Fc8v6yWxD14GQMnV fight 19 @ 197.864 at 2.798s, @ 1613.444 at 2.563s). Effect
  1583 has no application tag requirements. Sources checked: `GA_Mara_StunSpender`, `GE_Mara_StunSpender_Stun` and
  `GA_Passive_StatusEffect_Stun`.
- **Stalker Step with no buff:** 1 of 248 casts has no effect 2190 (GXFz3pZ94aQr2chV fight 3 @ 219.444; the next cast 0.768s
  later has it). The failed-teleport branch at ubergraph offset 1941 fits it. `AuthTeleportCharacterToTarget` is native and
  undecoded, and the log gives no target, so no source says why this teleport failed. The engine has no positions and spends
  a charge and applies the buff on this cast. Sources checked: `GA_Mara_ChargedTeleportBehindTarget` and effect 2190's
  `IgnoreTags` `CharacterContext.Dead`.

- **A clone's sweep with a frame and no hit:** a clone's Arachnid Assault at times has a frame with no hit on an enemy that
  lives on and takes the other clone's hit (3M2vWr8taxVfTQFY fight 53 @ 172.949, enemy 98.3). Over the 23 Mara fights of
  `data/fights`, 212 engine clone hits have no log hit and 244 of 52,855 log hits have no engine hit, some of them both ends
  of one such frame. The clone tests no range and no line of sight per enemy. Sources checked: `GA_Mara_Passive_CopySpawner`
  offsets 120 (`WaitDelayCR` 0.01s, native), 3954 and 4200 (`TargetPassesFilters`, `CreateAoeTargetFilterCollection`:
  `AddFilter_CharacterContext`, native, and `AddFilter_Relationship`).

## Not yet checked against a log

Red Ledger, Corrosive Spill, Bloodrush, Efficient Killer, Gushing Blood, Maiden's Doom, Seething Burst, Hemotoxin,
Arachnid Onslaught, Caustic Wounds, Puncture, Final Stratagem and From the Shadows are built from their tooltips and the
game data (marked `UNVERIFIED` in their files). A log of a build with them would settle: the damage ID of Seething Burst,
what Hemotoxin's "bleed damage" counts, and how From the Shadows' clone is logged.
