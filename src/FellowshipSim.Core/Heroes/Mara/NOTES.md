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
- **Vexira's Venom:** the first crit gives ticks of 28% of it / 3. Crits and grievous crits count (`HitTypeIsCritical` exe
  0x14523BFF0), and the shadowy clones' crits add none: `GA_Mara_Passive_Talent_AoeAttackSpender_DamageToPoisonDot` offsets 251
  to 507 reject `CustomAssetTags.Mara.CopyCharacterDamage.Damage`, a dynamic asset tag in the event's `EffectAssetTags` (exe
  0x144A76EA0). A crit on a running venom gives (tick x ticks in the time left + 28% of the crit) / ticks in 6s, where ticks in s
  seconds, with the next tick r away and the last e ago, is 1 + (s - r) / 2 when s >= r and (s + e) / 2 when s < r, in unhasted 2s
  ticks and the period timer's real time. In the game data (build 26320967), offset 1187 calls
  `CrAddToAndRecalculatePeriodicEffect` (exe 0x14521C1D0), with this rule at 0x14521C6D8 to 0x14521C782. It moves the effect's
  start times (0x144A83160) and sets the magnitude, leaves the period timer alone and applies nothing, so
  `EffectLogic.CarryOverDuration` and its 0.3 limit (stacking re-applications alone, 0x1452D7CD0) give no carry-over, and the log has
  no refreshdebuff for effect 2364 (8,371 applydebuff, all duration 6000). 98.0% of the 16,849 refreshes with one crit between two
  ticks in the 19 Mara fights of `data/fights` with the venom are within 1% of this, and 99.4% within 3%, at 94.8% or more in each
  fight. T2qLPpmMkrAtJ9B1 fight 18 @ 31.671: a 1,787 tick, 3.931s left, r 1.401s and a 15,061 crit give 2,506; enemy 8.2 in that
  frame, a 2,023 tick and a 14,326 crit, gives 2,606. The ticks never crit (effect 2364's preset `Empty`).
- **Blocked hits** (hit type 4; 40 in fight 19, Queen's Fang and auto attacks): the logged amount is what the block
  left, so a replay compares the engine's hit with the amount plus the blocked part.
- **An enemy's damage reduction:** the log's `mitigated` on a hit is what the enemy took off it, the block included (on a blocked
  hit it is the blocked part and 1: Fc8v6yWxD14GQMnV fight 19 @ 32.008, blocked 29,604, mitigated 29,605), and amount and mitigated
  make up the log's `unmitigatedAmount`. gd6ByNrf81jc9aWw fight 3 (Silken Hollow) has 1,104 of Mara's 16,995 hits with 45% to 55%
  mitigated and no block, on every enemy hit at 260 to 280 s, 300 to 320 s, 360 to 380 s and 420 to 440 s (@ 303.867: Arachnid
  Assault on enemy 55.2, amount 2,488, mitigated 2,082), and 3tM2NRFDCnhkqdx6 fight 7 (Silken Hollow) has 185. The log over the
  engine there was 0.823, 0.598, 0.772 and 0.856 against 0.97 to 1.05 in the other windows. A replay compares the engine's hit with
  the amount plus the mitigated part, and a hit deals the amount: a crit's damage over time takes a share of that amount (Amethyst
  Splinter and Vexira's Venom in fight 3 are 0.944 and 0.952 of a share of the unmitigated amount, and 1.000 and 1.001 of a share of
  the amount). Fight 3's exact replay goes from 104.1% of the log to 99.9%.

## Timing

- **Global cooldown:** 1s, not hasted (casts 1.032s apart at the least, during Spirit of Heroism too). A mount waits
  for none: `GA_Mount_MountUp_Base` has no `GlobalCooldownGameplayEffectClass`, and its begincast comes inside the global
  cooldown of Arachnid Assault (`GE_GlobalCooldownStatic`): 3M2vWr8taxVfTQFY fight 53 @ 109.634, 0.43s after it;
  dBgbn8wD1RxL7Kma fight 9 @ 145.358, 0.40s after it, and @ 275.670, 0.30s after it; X82J6bGRNBracxLg fight 7 @ 994.128,
  0.60s after it. The replay at fix-16 refused 37 of the 122 mount begincasts in the 23 Mara fights this way. That a mount
  applies none is UNVERIFIED: the native default on `CRGameplayAbilityMounting` is unread, and its 1.5s cast covers a global
  cooldown in the logs.
- **Auto attacks:** a 1.5s swing shortened by Haste (1.30s apart at 13% Haste, 1.035s during Spirit of Heroism); none
  in Stealth.
- **Stealth** (Brooding Shadows): ends with the next Backstab, Widow's Bite or Skittering Blades, 0.67s after it starts
  at the median; Kick and Stalker Step keep it. The cast spends no charge (`CooldownCommitmentPolicy` CCP_Manual:
  `CommitExecute` 0x1450DCB70 applies cost alone). `GA_Mara_Passive_StealthMonitor` `Remove Stealth Effect` spends it when
  it ends Stealth (offset 231, `K2_CommitAbilityCooldown(false, true)`, 0x1449DEE10): outgoing damage and incoming damage
  after a 2s window always, a Secondary skill in combat alone. A recast in Stealth ends it and spends no charge
  (`GA_Mara_Stealth` offsets 103 to 266): all 12 recasts in the 23 Mara fights of `data/fights` log the removal in the frame
  of the cast, with no application (T2qLPpmMkrAtJ9B1 fight 18 @ 800.613, next cast @ 808.295; 3M2vWr8taxVfTQFY fight 53 @
  318.142, next cast @ 319.238). `Mara.Stealth.PaddingCooldown` is 0.7s / Haste between casts, in real time from the Haste at the cast
  (`GE_Hero_Shared_PaddingCooldown`, `GEC_HasteMagnitude`, 0x1450E2F8A-0x1450E2FA0). A mount removes Stealth with no
  charge spent: `GE_Mara_Stealth` IgnoreTags `CharacterContext.Mounted` (WcTBrQNX46m9axqP fight 7 @ 517.728; 3 more
  removals beside a mount cast).
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
- **Hemorrhaging Strike:** no cooldown. The 1s global cooldown limits a recast, and Combo Points and 20 Energy are its other needs.
  In the game data (build 26320967), `GA_Mara_BleedAttackSpender/defaults.json` has `CooldownDuration` with the row
  `Mara.BleedAttackSpender.Cooldown` and no `Value`, table or registry, and no `CooldownEffectBaseClass`. Constructor 0x1450D7D60 gives
  them `Value` 0 and null, evaluator 0x144AA85B0 returns that `Value` with the row's 2 unread, and `ApplyCooldown` 0x1450D9990 makes no
  spec. The export's `cooldown` 2 is that row, read by name. `FellowshipCommitHeroAbility` 0x1450E0060 still applies the global cooldown
  and `GE_Hero_Shared_PaddingCooldown`, 0.3s (`Hero.AbilityPaddingCooldown.Default.Duration`) from `GEC_HasteMagnitude`. The engine models
  padding for Brooding Shadows alone, and 0.3s sits under the global cooldown. T2qLPpmMkrAtJ9B1 fight 18 @ 1062.844 and 1063.878: 1.034s
  apart on Noor, The Betrayer, a hit each (4,146 crit, 2,326 normal), the second spending the 5 Combo Points of the first's Spirit Refund.
  Of the 228 gaps between casts in the 23 Mara fights of `data/fights`, 2 are under 2s (also Fc8v6yWxD14GQMnV fight 19 @ 321.816, 1.934s),
  5 at 2s to 3s, 3 at 3s to 4s and 218 at 5s or more, with no Final Stratagem cast in any fight.
- **Hemorrhage:** ticks every 3s shortened by Haste, with a partial tick as it runs out. A refresh keeps the tick timing,
  and up to 30% of the new duration of the time left carries over.
- **Seething Poison:** ticks every 2s shortened by Haste; its 60s are not hasted.
- **Volatile Poison:** each application and refresh rolls a duration of 6 to 8 seconds, the ticks are a sixth of it apart, shortened
  by Haste, and the poison ends with a partial tick. In the game data (build 26320967), `GMMC_Mara_AoeAttackBuilder_Poison_DurationManager`
  rolls `MinRandomDuration` to `MaxRandomDuration`, and `GA_Mara_Passive_Stealth_Poison_Manager` offsets 354 to 552 set the tick period
  scaler to the duration / `MaxTicks` 6 on each application. `SetGameplayEffectCustomTickPeriodScaler` (exe 0x145243620) stores
  `SetByCaller.System.TickRateOverride` and re-arms the period (0x1452F2A70, 0x1452DC7E0): new period = Period / Haste x scaler, first
  delay = new period x remaining / old rate, no tick. A re-application replaces the spec, rolls the duration again with no carry-over,
  since the effect lacks `EffectLogic.CarryOverDuration`, and leaves the period timer (`StackPeriodResetPolicy` NeverReset, 0x144A6A660),
  and the applied delegate re-arms it with the new scaler. Each tick reads Mara's attributes at the tick
  (`RecaptureSourceAttributesBeforeEveryExecution`, 0x144A808C4). The 10,664 durations in the 23 Mara fights of `data/fights` are spread
  evenly from 6.000s to 7.999s. T2qLPpmMkrAtJ9B1 fight 18 @ 28.264: one cast gives enemy 8.1 6.089s with ticks 0.87s apart and enemy 8.2
  7.980s with ticks 1.14s apart. Over the 181 refreshes where the rescaled time to the next tick and the old period's are 0.1s or more
  apart, in 22 fights, the first tick is within a server frame of the rescaled one 73 times, of the old period's 14 times and of a
  restarted period's 19 times (b649dhNptyW132Tm fight 5 @ 92.110: durations 6.246s to 6.719s, ticks 0.97s to 1.03s apart). The partial
  tick reads the period timer on the frame CheckDuration runs (0x144A6FAB0, 0x1452D4B70): read as the duration runs out, the 7,615
  partial ticks are 1.075x the engine's at the median, and read on that frame 1.028x.
- **Volatile Poison's eruption:** a natural expiry alone erupts. `GA_Mara_Passive_AoeAttackBuilder_Poison_Aoe` offset 37 cancels on
  `bPrematureRemoval`, false from CheckDuration and true from `EffectLogic.RemoveOnDeath`'s removal (0x144A80C20), and a refresh removes
  nothing. Offset 75 delays it `RandomFloatInRange(0, 0.2)`, and offset 1122 applies effect 1730 to the poisoned enemy alone. A sim rolls
  the delay, and a replay lands the hit at the log's time. The 8,218 eruptions come 0.008s to 0.228s after the removal. Of the 1,182
  removals with no eruption, 1,132 are within 0.1s of the enemy's death, 38 come before the duration ends, and 9 come as it ends with
  the enemy dying up to 0.2s later, inside the delay.
- **Shadowy clones:** the first strikes 0.335s after the cast, the second 0.335s after the first is done; a clone's
  Arachnid Assault hits one enemy per server frame, in an order of its own (329 of 1,017 clone pairs in the same order).
- **Vexira's Venom:** ticks every 2s shortened by Haste, with a partial tick as it runs out; a new crit restarts its 6
  seconds and keeps the tick timing. The 6,304 removals after a tick in their frame are 6.01s to 6.03s after the last crit, and
  the partial tick reads its progress on that frame. A tick follows the log's tick (`IOutcomeSource.TickAt`): with the
  free-running timer 562 engine ticks had no log tick, and on the period timer of an effect tagged EffectType.Dot 226 have none.
  Effect 2364 has `EffectType.Dot`, no `EffectLogic.TickrateNotAffectedByHaste` and no `EffectLogic.IgnorePartialTicks`.
- **Amethyst Splinter:** ends with a partial tick 8s after the last crit, and the next crit starts a new rhythm. In the 23 Mara
  fights of `data/fights`, 583 of the 799 removals off a death are 7.9s to 8.1s after the last crit, and 569 of the 799 have a tick
  in their frame. T2qLPpmMkrAtJ9B1 fight 18 @ 53.144: a tick of 673 after ticks of 985, 1.17s after the last, with the removal
  in the same frame and the new crit at 53.712. The 558 partial ticks with a tick before them are 0.098s or more after it,
  and the 26 removals 0.070s or less after a tick have none, on `MinimumPartialTickTime` 0.1 (`DefaultGame.ini`). In the game
  data (build 26320967), effect 101 has `EffectType.Dot`, a crit on a running effect sets its value to (crit x share + left x
  value / 2) / 4 and refreshes its duration with the period timer left alone (`GA_Trait_GemDotHotOnCrit` offsets 493 to 1428,
  exe 0x145241E40 resets the duration timer alone), the next tick reads the new magnitude (0x144A911F0), and a crit after it ran
  out applies a new effect (offsets 1861 to 2201). CheckDuration (0x144A6FAB0, 0x1452D4B70) gives the partial tick, factor elapsed
  / period, skipped under `MinimumPartialTickTime`. A tick follows the log's tick (`IOutcomeSource.TickAt`), so one missing log
  tick shifts no later pairing: with the free-running timer, ticks 0.15s behind the log paired with the next log tick, up to
  1.05s late at the 90th percentile in T2qLPpmMkrAtJ9B1 fight 18.
- **Amethyst Splinter on Mara's death:** effect 101 has `EffectLogic.RemoveOnSourceDeath`. X82J6bGRNBracxLg fight 7: Mara dies at
  690.024 and the splinters leave at 690.026 with no partial tick, and ending them there took 249 engine-only ticks off the 23
  fights (646 to 397).
- **Haste on a running Amethyst Splinter:** each Haste change of Mara's re-arms the running period with the fraction left carried
  over, no tick and no partial tick, as `HastedTimer.Update` does. In the game data (build 26320967), the period is divided by the
  instigator's Haste at application (0x144A6B413 to 0x1452D5C70), and the Haste attribute's change delegate reaches every live Dot
  or Hot effect Mara applied (0x1452EFC60, 0x1452F2830, 0x1452F2A70) through the re-arm 0x1452DC7E0: new period = Period / Haste, first
  delay = new period x remaining / old period. T2qLPpmMkrAtJ9B1 fight 18, enemy 6.1, applied at 22.886 and removed at 53.144: ticks
  1.87s to 1.90s apart at 24.8 to 28.6, 1.70s to 1.74s at 30.4 to 39.0, 1.84s to 1.90s at 39.0 to 50.2. Prvc4XmbdHBaVRKy fight 12,
  enemy 78.9, applied at 809.717 with no removal by 855: 1.67s to 1.70s at 811.5 to 818.2, 1.84s to 1.87s at 818.2 to 827.5, 1.67s
  to 1.70s at 827.5 to 834.3.
- **Bloodrite Fervor** (ability 185): usable during the Global Cooldown, starting none, on a 90s cooldown of its own. Over the 20
  casts in the 34 Mara fights of `data/fights`, Mara casts it 0.77s after Skittering Blades (3M2vWr8taxVfTQFY fight 53 @ 89.555),
  Stalker Step comes 0.30s after it (fjrCax7VzGmMZw6h fight 26 @ 67.411), and the shortest gap between two casts is 90.1s
  (b649dhNptyW132Tm fight 5 @ 517.886). In the game data (build 26320967), the cooldown is `RelicPower.PartyMoveSpeedBuff.Cooldown`
  times the relic cooldown gem factor (`GMMC_GemAugmentation.CalculateBaseMagnitude`: 1 - 0.08 with Blessing of the Artisan, 1 - 0.24
  with its rank II), and effect 156 lasts 12s with MoveSpeed x1.5 alone. Before it, the replay skipped the 20 casts as unmodeled.
- **Conjure Portal** (ability 1504): a 1.5s Global Cooldown of its own, shortened by Haste, and a 120s cooldown (game data, build
  26320967). Over the 10 casts in the 34 Mara fights of `data/fights`, the next cast on the Global Cooldown comes 1.139s after it at
  the least (Skittering Blades, Hwxc8TRFqPGhfjbN fight 5 @ 446.832) and 1.438s (HgamNcX24tJ3qVQA fight 21 @ 691.259). Relic Teleport
  comes 0.636s and 0.804s after it (Zta3LQdvVbPJ9Whz fight 6 @ 187.280, GXFz3pZ94aQr2chV fight 3 @ 463.138), inside that Global
  Cooldown, and the replay forces both casts.
- **Essence of the Virtuoso from Anomalous Residue:** with no Topaz power in the gear, each Residue: Topaz window gives effect 1937
  in the Residue's frame, and Spirit of Heroism takes it off and gives it back. T2qLPpmMkrAtJ9B1 fight 18 @ 383.829, 530.946 and
  1400.233; Fc8v6yWxD14GQMnV fight 19 @ 809.379, 958.352 and 1120.143. In the game data (`DT_GemDefinitions`, Topaz row), the node
  grants `CA_GemBonus_HastePercentageBonusWhileNotHeroismSmall`, and effect 1937 is Infinite, with `OngoingTagRequirements` IgnoreTags
  `CharacterStatus.SpiritBuffed`. `UCRGemPowerComponent`'s handler (exe 0x145398640) grants a node's augmentation at every rise past
  its `RequiredGemPower` and removes it at every fall below. Without it from the second window on, Mara's Haste was 0.02
  low: Seething Poison ticks 1.832s apart against the log's 1.803s at T2qLPpmMkrAtJ9B1 fight 18 @ 420 to 430, and Hemorrhage ticks
  over 0.15s behind the log, with no pair, left their crits' Vengeful Soul gains out (430.757, 1422.421; Fc8v6yWxD14GQMnV fight 19
  @ 1166.032). Each activation of the node gives the Essence: 21 more Hemorrhage hits matched and Vengeful Soul's issues over the
  23 fights went from 8 to 4.
## Resources

- **Energy:** 200 at most, full at the start. It regenerates at 15 per second x (1 + Haste), times 1.2 with Predator's Rush and
  1.2 with Maiden of Death. Between neighbouring log readings in the 23 Mara fights of `data/fights`, log over this rate has a
  median of 0.997 to 0.999 in all four buff states, over 11,258 pairs. In the game data (build 26320967), `GA_Mara_Passive_EnergyManager`
  has a looping timer (TickAsync, offset 1899, 0x14511CE40) of `Mara.Energy.GainInterval` 0.01s / Haste, / 1.2 with Predator's Rush.
  Each tick gains `Mara.Energy.GainAmount` 0.15 through `MaraUtils_GainEnergy`, which multiplies it by 1.2 with Maiden of Death
  (offsets 232 to 507). The timer fires as many ticks as fit in a frame (FTimerManager::Tick 0x144058163 to 0x1440583B0).
  Regeneration has `IsPassiveRegen`, so it does not log.
- **Energy gains and Maiden of Death:** Hemorrhage's ticks and Efficient Killer go through `MaraUtils_GainEnergy`, so Maiden of Death
  multiplies them. Widow's Bite (offset 586), Venomous Delight (`HeroUtils_ModifyResource`, offset 729) and a Spirit Refund's Energy
  (`HeroUtils_ModifyResourceDelayed`, 0.2s, offset 1207) do not go through it.
- **Energy at a change of Haste or Predator's Rush:** the change restarts the regeneration timer, and one server frame of
  regeneration is lost. `GA_Mara_Passive_EnergyManager` watches Haste (offset 856) and the Predator's Rush tag (offset 2284). Either
  ends the timer (offset 691) and starts a new one after `DelayUntilNextTick` (offset 732), TickAtStart false, so the frame and the
  old timer's remainder are lost, and a second change in that frame finds no watcher. Maiden of Death has no watcher. The size is a log pattern: the remainder and the order of the timer and latent ticks in a frame have
  no decoded value. Over the 8 players with no Venomous Delight (3M2vWr8taxVfTQFY fight 53, b649dhNptyW132Tm fight 5, gd6ByNrf81jc9aWw
  fight 3, mThk4v1f8WGbBd7Q fight 1, p8VJDcBdxa7ghqG3 fight 124, T2qLPpmMkrAtJ9B1 fight 18, v8rhNxVc3PwDqzBA fight 4 player 28,
  w7khnMKAa4TxXrmC fight 23), with no frame lost, log minus engine Energy moves 0.035 per second over 4,666s with no change, -0.64 at
  a Haste drop (478 steps), -0.85 at a Haste rise (383), -0.63 and -0.79 at a Predator's Rush change (59 and 38): 0.02s to 0.04s of
  regeneration at rates of 15 to 30 per second. A step with Maiden of Death alone moves +0.22 as it ends (235) and +0.01 as it comes
  (101). With the frame, those steps move +0.17, +0.08, -0.02 and -0.07, and +0.20 and +0.04 for Maiden of Death. A step with one
  source of Haste changed moves -0.10 and +0.09 for The Philosopher (598 and 449 steps), -0.05 and +0.01 for Inspired Allegiance (175
  and 187), and -0.14 and +0.09 for Spirit of Heroism (97 and 51). T2qLPpmMkrAtJ9B1 fight 18 @ 748.881: The Philosopher and Maiden of
  Death come, and log minus engine goes from 0.13 to -0.86 by 749.152 with no frame. 3M2vWr8taxVfTQFY fight 53 @ 143.227 to 144.530:
  Maiden of Death and a Haste change leave it at 0.53 and 0.47 with the frame. A Trait : Focused Haste or Inspired Allegiance change
  of the engine's with no log event restarts the engine's timer alone: Focused Haste has 986 engine changes against 342 log events in
  3M2vWr8taxVfTQFY fight 53 and 761 against 244 in Prvc4XmbdHBaVRKy fight 12, and Inspired Allegiance 944 against 103 over the 23 fights.
- **Spirit Refund:** a finisher's refund gives back its Combo Points and its Energy (fight 19 @ 25.691: 6 Combo Points
  and 40 Energy), and 1 Spirit Point.
- **Maiden of Death's Combo Points need a hit:** in the game data (build 26320967), each generator's `EventReceived` callback
  on `Event.Damage` calls `BFL_Mara_Utils.MaraUtils_GainComboPoints`, which refills to `MaxResourcesSecond` with
  `CustomAssetTags.Mara.ChargedResourceRefillSelfBuff` active (`GA_Mara_AoeAttackBuilder` offsets 1908 to 2302, the utility's
  offset 133). Over the 8,255 generator casts in the 23 Mara fights, 33 have no hit, all
  Skittering Blades, and each costs its Energy and leaves Combo Points unchanged. 7 of the 33 are inside Maiden of Death:
  T2qLPpmMkrAtJ9B1 fight 18 @ 1139.361, 1264.047 and 1515.854, v8rhNxVc3PwDqzBA fight 4 player 28 @ 582.959 and 678.261,
  xkGbdm8aNDwTcgjF fight 17 @ 289.281, xkGbdm8aNDwTcgjF fight 2 @ 220.343. Skittering Blades does not travel: its 46,053 log
  hits land 0.017s after the cast at most. A hit reads Maiden of Death as it stood at the cast: v8rhNxVc3PwDqzBA fight 4
  player 28 @ 183.304 has the hit 1ms and the buff's end 4ms after the cast, and 6 Combo Points.
- **Spirit Points from health lost:** each player gains SpiritPointValue x the fraction of the enemy's health lost x 0.25, from the
  party's damage. In the game data (build 26320967), `GA_Shared_Passive_SpiritMechanic` offsets 626 to 888 compute SpiritPointValue x
  min(MaxHealth, |TotalHealthChange|) / MaxHealth x `Hero.SpiritPointGain.SpiritPointsFromDamageScaler` 0.25 on each `Event.Damage`
  (offset 1189), offset 145 skips a heal, and a healed enemy gives Spirit Points again. The ability keeps no state per enemy, and offset
  1079 applies the gain to `AcquireTargets_AllPartyAndRaidMembers`. `GEC_Shared_SpiritPointGainCalculation` offset 173 multiplies it by
  4 under `ShouldGiveDungeonTimerExpiredBonus`, which the engine has no model of. T2qLPpmMkrAtJ9B1 fight 18, Ancient Koros (value 45,
  18,873,188 health): Mara's hits of 180,371, 139,767 and 96,669 raise Spirit 0.11, 0.09 and 0.06, a quarter of each hit's share of the
  value. Health lost to the party counts: Mara's own damage x value x 0.25 gives a quarter of the old sum (T2qLPpmMkrAtJ9B1 fight 18:
  34.38), and the log's small gains are 0.36 to 0.97 of that sum in the 23 Mara fights of `data/fights` (T2qLPpmMkrAtJ9B1 fight 18: 85.89
  of 137.51). In the binary, each member's `WaitForCombatEvents` hears that member's outgoing damage alone (exe 0x1451260E0, raiser
  0x1452E4900, with the call into the raiser untraced), and `AcquireTargets_AllPartyAndRaidMembers` returns every living, targetable member,
  the owner included (0x1450F96D0). A shadowy clone's hit gains like the cast's: its spec's context names Mara (`GA_Mara_Passive_CopySpawner`
  offset 4412 `CloneSpecHandle`), and isolated clone Queen's Fangs on bosses rise 0.68 for a rule of 0.50 over 16 hits in 4 fights
  (T2qLPpmMkrAtJ9B1 fight 18, Fc8v6yWxD14GQMnV fight 19, pWmcV42K39GHTLQX fight 23, mThk4v1f8WGbBd7Q fight 1), where cast hits are 1.11x to
  1.24x the rule. `TotalHealthChange` includes overkill and absorbed damage and excludes blocked damage (0x1450DD4C8 to
  0x1450DD6F9), so a killing blow counts whole up to MaxHealth. The engine reads health and has no overkill. A raw fight has one player's
  damage alone, so a replay cannot know the party's overkill. Mara's 790 killing blows in the 23 fights (hit points wrapped under 0) add 3.70
  Spirit Points in all, 0.64 at most in a fight and under 0.2 each, inside the 2.0 tolerance, so the engine leaves it out. Spirit Points stay
  in [0, MaxSpiritPoints] (0x1450F0DB5), as `Hero.AddToResource` does, and a creature with no row has value 0 (0x14503C7EE), as
  `SimulationContext.GetOrSpawnEnemy` and `EnemyGroup.Spirit` do. Over the 23 fights the replay's
  `Spirit too high` goes from 404 to 71 issues, with 35 `Spirit too low`. T2qLPpmMkrAtJ9B1 fight 18 goes from 25 issues to 3.
- **Deadly Scheme:** a stack per 5 Energy spent, and the stacks past 40 carry over. `GA_Mara_Passive_Talent_ResourceAttackSpenderCritChance`
  keeps the count in its own variables and sets it on effect 2434 at each spend (offset 3094), Active or not. Effect 2434 is off while
  effect 2068 is on (`OngoingTagRequirements` IgnoreTags on the tag of 2068) and holds its stack count, clamped at 40: no logged stack is
  above 40 in the 23 Mara fights, and T2qLPpmMkrAtJ9B1 fight 18 goes 39, 48 past the clamp, then 15 @ 548.345 for 8 carried and 7. The
  log shows the effect's switch as `removebuff` and `applybuff`. Under Active a falling count logs `refreshbuff` with no number, a rising
  count `applybuffstack` (fight 18 @ 36.846, then 21 @ 37.881, 12 + 9 stacks), and the `applybuff` at the end has no number after a spend:
  3,335 of the 3,341 uses and 8 of the 20 run-outs in the 23 fights. A number-less `applybuff` is no reading of 1. `InhibitedAuras` reads these events:
  the buff is off under Active, comes back with the last number, and a return with none reads as the engine's until the next number.
  With no spend during Active, the run-out brings it back at 40 (12 of 20, fight 18 @ 544.776), and the next spend shows the count.
  The stacks given to the hero set the count: GXFz3pZ94aQr2chV fight 3 starts with 7 stacks held, its first spend showing 14 for a 35
  Energy cost, and a replay's sync brings the count level. Over the 23 fights `Deadly Scheme` goes from 4,972 issues to 1, that fight's
  first check.
- **Venomous Delight:** a proc is a 10 Energy jump right after a poison hit (172 jumps of 8 to 12 in fight 19). A proc
  at full Energy shows nothing. With the rate above, the jumps between neighbouring readings up to 1.5s apart in the 23 Mara fights
  of `data/fights` are 5,940 of 8 to 12 with the talent and none without it, and 216 of 18 to 22 (13 without it), two procs in
  one jump. A reading at a cast is before its cost, and 245 of the jumps fall at readings 0.5s to 1.5s apart: not every damage
  event has a reading. A replay proves a proc from these rises with a cast's cost counted back, at the rate at the roll, and a
  rise from a cast's reading after that cast, since the replay checks Energy at the cast's reading: 5,086 procs proved of 9,383
  expected became 7,209. In the game data (build 26320967), `GA_Mara_Passive_PosionDamage_Monitor` rolls on every damage event with
  `EffectType.Poison` or `EffectType.Magic.Poison`, Vexira's Venom too, with no internal cooldown and no delay. The roll is
  `ControlledRandomBool` (0x1452C6800), one sequence per stream: A = A x T[10] 0.984358 at each roll, reset at a success, so the k-th
  roll after a success has 1 - 0.984358^k and the rolls per success average 10. The engine's sims roll a plain 10%.
- **Malevolence:** a finisher uses one stack of its own buff, then grants the other.
- **Rising Spirit:** each further stack adds 2s, and the buff ends 20s after its application at the most. Over the 1,303 buffs in the 23
  Mara fights of `data/fights`, the buffs running out are 10s + 2s per further stack, 18.013s at the median at 5 stacks and 20.011s at 6,
  and none is over 20.1s: the 22 at 7 and 8 stacks run 19.970s to 20.041s. T2qLPpmMkrAtJ9B1 fight 18: applied 95.261, 7 stacks by 114.065,
  removed 115.267; applied 219.100, 7 stacks by 233.928, removed 239.104; applied 1356.138, 8 stacks by 1374.943, removed 1376.145. In the
  game data (build 26320967), `GA_Shared_Passive_SpiritRefund_BuffDurationManager` starts from the effect's total, 10s, and on each stack
  adds `Hero.SpiritProcRefundResource.Buff.DurationIncreasePerStack` 2 (offsets 1870, 2122). At `MaxDuration` 20 it ends its stack-change
  task (offset 2342), so a later stack comes with the expiry unchanged. Effect 3115 has `StackDurationRefreshPolicy` `NeverRefresh`, and a
  new application after a removal starts from 10s again. The log's `duration` on `applybuffstack` shows the 2s of every second stack:
  stacks 2, 3, 4, 5 show +2, +0, +4 and +0. The removal fits 2s per stack (applied 137.607, 3 stacks, logged expiry 149.605, removed
  151.636), and the graph adds 2s on every stack. The field's writer is native and unread, so the alternation has no source.
- **Emerald Judgement:** the spike lands with the hit that rolled it, on that hit's enemy. In the game data (build 26320967),
  `GA_Trait_GemTargetedSpikeProc` rolls `RollProcPerMinute` on `Event.Damage` (offset 297) and applies the spike in the same graph run
  (offset 758) with no delay task, on the event's `Target`. Over the 443 spikes in the 23 Mara fights of `data/fights`, 441 are logged in the
  row before a hit on their enemy. T2qLPpmMkrAtJ9B1 fight 18: 137.946 on
  8.1 with a tick of Amethyst Splinter, 724.639 on 6.5, 1386.370 on 22.1. A log spike proves a roll a frame either side of it: a roll up to
  1s before, as the replay took it, gave the spike's First Strike to the hit that rolled it, and those hits' log amounts have none
  (GXFz3pZ94aQr2chV fight 3 @ 175.855: Queen's Fang 14,473 in the log, 15,978 with First Strike and 14,609 without).

## Auras

- **Inspired Allegiance from another player:** the log's state of an aura on Mara is her own: another player's buff on her is an
  external aura of the replay, with that player as its source. Another player's Inspired Allegiance and Mara's own are two
  instances, each with its own removal: CwmX2Tb8GA6hyc1v fight 8 has the other player's @ 20.204 to 28.219 and Mara's own @ 22.205
  to 30.223. The replay's check reads Mara's own instance against the log: with both counted, the 2 Mara fights of `data/fights` with
  the sigil had 678 `Inspired Allegiance extra in engine` issues (496 in CwmX2Tb8GA6hyc1v fight 8, 182 in w7khnMKAa4TxXrmC fight 23),
  and with Mara's own alone they have none.

## Not yet explained

- **Emerald Judgement on a later hit of one instant:** 40 `proc with zero chance` issues over the 23 Mara fights: the spike's hit is a
  later hit of a millisecond in the engine's order, and the roll before it in that millisecond leaves `t` 0. In the log, the frame's first
  damage row is on the spike's enemy for 430 of 442 spikes, and the replay's order in one millisecond is the engine's. CwmX2Tb8GA6hyc1v
  fight 8 @ 82.187 (spike on 87.2, Amethyst Splinter on 87.5 first in the engine), @ 946.407 and @ 1141.426; d1LvbCpBWXRM8Hcm fight 14 @ 244.791.
  At `t` 0 the game's chance is 0 after the stream's first proc and `R / 8` before it (`RealPpm`): 2 of the 40 were a
  fight's first spike (dBgbn8wD1RxL7Kma fight 9 @ 14.965, v8rhNxVc3PwDqzBA fight 4 @ 12.026). Sources checked: `GA_Trait_GemTargetedSpikeProc`,
  `RollProcPerMinute` 0x1452CBD20 (settings `procPerMinuteRoll`). Its `t` is the frame's world time (0x143A19920), so a later roll of a frame
  has `t` 0 and only the frame's first roll procs after the first proc. The engine's first roll of a millisecond is on another enemy
  in these 38, and the log has no field for the game's order within a frame beyond its rows.

- **A generator in Maiden of Death's last frame:** 5 `Combo Points too low` issues over the 23 Mara fights, one each: a generator
  cast 10.017 to 10.023s after the buff's application and 1 to 3ms before its removal gets the full refill in the log, and the
  engine's buff has ended, at 10.0167s (`AuraContainer.ExpiryLagSeconds`). T2qLPpmMkrAtJ9B1 fight 18 @ 1389.776 (engine 4, log 6),
  CwmX2Tb8GA6hyc1v fight 8 @ 1321.340 (1, 6), Prvc4XmbdHBaVRKy fight 12 @ 1259.330 (2, 6), gd6ByNrf81jc9aWw fight 3 @ 544.840 (5, 6),
  zgxBpVKw2m71cDdk fight 5 @ 705.439 (5, 6). In 927 of the 937 buffs the removal is 10.002 to 10.043s after the application, mode 10.020. A Widow's
  Bite 31ms after the removal gains 5, no refill (T2qLPpmMkrAtJ9B1 fight 18 @ 335.423), and log ID 1001714 has no event in the
  two fights checked (T2qLPpmMkrAtJ9B1 fight 18, xkGbdm8aNDwTcgjF fight 17). Sources checked: effect 2194's duration 10, the asset
  `GE_Mara_Talent_ChargedResourceRefillSelfBuff_IncreasedDamage_Buff` (`HasDuration`, no period, no field for expiry timing),
  `DefaultEngine.ini` (no tick rate setting), and the binary (build 26320967): the effect's duration timer calls `CheckDuration`
  0x144A6FAB0 from `FTimerManager::Tick`, on the first Tick past the expiry, so the removal is at the frame boundary after the 10s.
  The refill's query 0x14523B630 matches child tags, so effect 1714 counts as 2194 does, and `EffectDelay` 0.35 delays no
  application. `UWorld::Tick` 0x143B79680 dispatches a client's RPCs (0x143B797DC) before `FTimerManager::Tick` (0x143B7A097), so a
  generator activated in the frame of the expiry reads the buff as present. The engine has no server frame grid and ends the buff
  at the mean lag, and no tick rate has a source: the client binary and its config state none.
- **Spirit Points in two fights:** 50 of the 106 Spirit issues over the 23 Mara fights of `data/fights` are in two fights.
  Fc8v6yWxD14GQMnV fight 19 has 24 `Spirit too high`, net -78.91, around Emissary of the Shadow Lord (value 400): @ 283.401 the engine has
  66.05 and the log 59. gd6ByNrf81jc9aWw fight 3 has 23 `Spirit too low`, net +84.68, with rises of 2 or 3 every 0.5s for 2s (@ 58.117 to
  59.653, 146.978 to 147.846, 198.244 to 199.947). The Emissary's 400 x 0.25 is 1 Spirit Point per 1% of its health, so the 7.05 gap
  is 7.05% of its health, with the log at 46.67 @ 280.629 and 59 @ 283.401, under a maximum of 130. From 0.354 of its health @ 278.224 to
  0.019 @ 284.442 the log rises 26.88 with regeneration and Spirit Refunds in it, and the rule gives 33.5. Overkill counts the other way, since it adds to the log. Health lost to a
  source that is its own instigator reaches no Spirit listener (0x1450DD6D6), and a raw fight has no other player's damage to test this.
  Sources checked:
  `GA_Shared_Passive_SpiritMechanic`, which has no creature branch, creature 274's `spiritPoints` 400 and its passives 155, 120, 2242,
  2118 and 2835, none changing `SpiritPointValue`, `MaxHealth` or the health change, `CT_CharacterScalingBaseAttributes`, which has
  no Spirit row, and the enemy health samples.
- **Spirit Point regeneration's start:** the engine's timer starts with the fight, and T2qLPpmMkrAtJ9B1 fight 18 has 5 Spirit Points @
  22.884 against the engine's 7. Over a fight the log's +1 rises are 3.00s to 3.02s apart, with a phase moving about 3s in 1,500s. Sources
  checked: `Default.CRHeroAttributeSet.SpiritRegenPeriod` 3 and `Hero.Spirit.PassiveGainPeriod` 3. No Blueprint in `DS_MaraAbilitySet` runs
  the timer, and no native code reads `SpiritRegenRate` or `SpiritRegenPeriod` (`CRHeroAttributeSet` +0x188 and +0x198), so Blueprint or
  effect data runs it.

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

- **Amethyst Splinter ticks with no log tick:** 397 engine-only ticks over the 23 Mara fights of `data/fights`. 245 fall after a
  log removal with no death event, with no death of Mara's and before the 8 seconds end (CwmX2Tb8GA6hyc1v fight 8 @ 279.177: every
  debuff on enemy 15.1 leaves in one frame at 318,138 health). 119 fall in a running splinter with no log tick in its period, on
  several enemies in one frame (X82J6bGRNBracxLg fight 7 @ 47.604). Sources checked: the raw events on those enemies and effect
  101's tags. The log gives no reason for the removals.
- **Amethyst Splinter tick times:** 10,269 log ticks are outside the engine's predicted time by 33ms or more. Over the 22 fights
  with a tick offset, 19 have a median of +35ms to +54ms (log later) and 3 a median of -54ms to -169ms (Prvc4XmbdHBaVRKy fight 12:
  +46ms, 99% late). `HastedTimer` predicts from the tick before. Sources checked: `HastedTimer.Update`, and Vexira's Venom on
  the same enemies, whose log ticks fall behind the engine's in the same stretch (Prvc4XmbdHBaVRKy fight 12 @ 812 to 825: +0.03s
  to +0.17s).

- **Vexira's Venom partial ticks:** the 6,256 matched partial ticks are 1.0128x the engine's in sum, 31.6% within 1%, against
  0.9998x and 98.0% for the 59,139 full ticks. Sources checked: effect 2364's period and duration, and the removal times.
- **Vexira's Venom crit in the frame after its 6 seconds:** 29 crits up to 0.03s after the 6 seconds, in 9 fights, refresh the
  venom with no partial tick (T2qLPpmMkrAtJ9B1 fight 18 @ 68.775: a 7,370 tick and a 5,762 crit give 1,842, within 2.6% of the
  rule above with a negative time left). The engine's period timer is off by then, so a new venom starts and the old one deals
  its partial tick. Sources checked: `PeriodicEffect.BeginHasted`'s end events and `CrAddToAndRecalculatePeriodicEffect`
  (exe 0x14521C1D0).

- **Volatile Poison tick times:** 7,429 log ticks are outside the engine's predicted time by 33ms or more. Over the 23 fights, 20
  have a median of +40ms to +49ms (log later) and 3 a median of -34ms to -88ms, the pattern of Amethyst Splinter's tick times above.
  A log tick 40ms late pairs at times with the next: 513 full ticks are 0.505x the engine's at the median, where the engine's last
  full tick reserves the log's partial tick (CwmX2Tb8GA6hyc1v fight 8 @ 952.219, a 451 tick, and @ 952.320, a 72 partial tick).
  Sources checked: `HastedTimer.Update` and the period rule above.
- **Volatile Poison partial ticks:** the 7,615 matched partial ticks are 1.028x the engine's at the median, against 1.0023x for the
  65,989 full ticks. Sources checked: the duration and the removal times.
- **Volatile Poison refresh in the frame after its duration:** a refresh up to 0.003s after the duration ran out keeps the ticks
  (3M2vWr8taxVfTQFY fight 53 @ 546.738: duration left -0.003s, a tick 0.370s later). The engine's period timer is off by then, so 118
  log ticks after a refresh with 0.05s or less left have no engine tick.
- **Energy gains with no proof:** 679 `Energy too low` and 33 `Energy too high` rows remain over the 23 Mara fights of `data/fights`,
  212 of the cast steps at +10 from the step before. A Spirit Refund and a proc in one rise stay unproved: 54 rises of 48 to 57 above
  regeneration with the talent and none without it (Fc8v6yWxD14GQMnV fight 19 @ 623.634 to 623.706: 49.92, Prvc4XmbdHBaVRKy fight 12 @
  25.885 to 26.085: 55.10). In T2qLPpmMkrAtJ9B1 fight 18 the Energy finding has 25 issues (19 low, 6 high), net +46.95 of gross 88.47,
  Attack +24.08 over 14 steps. Sources checked: the rises in `LogOutcomeSource`, `GA_Mara_Passive_PosionDamage_Monitor` offsets 487 to
  729, and `GA_Mara_Passive_SpiritProcHandler`'s 0.2s delay.
- **Rising Spirit stack at its expiry:** 13 issues remain over the 23 Mara fights of `data/fights`, from 38, in 6 fights, each at a Spirit
  Refund within 57ms of the expiry. In 5 of the 6, the log's buff has the stack after the first application's 10s + 2s per stack have run
  out, and the engine has a new buff at 1 stack with 10s. X82J6bGRNBracxLg fight 7: applied 631.715, stack 4 @
  645.740 on an end of 645.715, removed 647.744. pWmcV42K39GHTLQX fight 23: applied 856.130, stack 4 @ 870.146, removed 872.158.
  d1LvbCpBWXRM8Hcm fight 14: applied 218.906, stack 4 @ 232.963, removed 234.970. xkGbdm8aNDwTcgjF fight 2: applied 605.639, stack 2 @ 615.669,
  removed 617.642. xkGbdm8aNDwTcgjF fight 17: applied 365.457, stack 8 @ 385.489, removed 385.495. 3M2vWr8taxVfTQFY fight 53 has the opposite
  order: the 12s end at 445.890, the log has a removal and a new buff at 445.896, and the engine's buff, ending at 445.907 with its lag, has
  the stack. Sources
  checked: `GA_Shared_Passive_SpiritRefund_BuffDurationManager` and `AuraContainer.ExpiryLagSeconds`. The engine has no server frame grid
  (see "A generator in Maiden of Death's last frame").
- **Volatile Poison with no eruption:** 3 removals at the duration's end have no eruption and no death within 0.2s (HgamNcX24tJ3qVQA
  fight 21 @ 722.081, X82J6bGRNBracxLg fight 7 @ 1487.378, Fc8v6yWxD14GQMnV fight 19 @ 806.549). Sources checked: the removal and death
  events on those enemies and `GA_Mara_Passive_AoeAttackBuilder_Poison_Aoe` offsets 37 and 1617.

## Not yet checked against a log

Red Ledger, Corrosive Spill, Bloodrush, Efficient Killer, Gushing Blood, Maiden's Doom, Seething Burst, Hemotoxin,
Arachnid Onslaught, Caustic Wounds, Puncture, Final Stratagem and From the Shadows are built from their tooltips and the
game data (marked `UNVERIFIED` in their files). A log of a build with them would settle: the damage ID of Seething Burst,
what Hemotoxin's "bleed damage" counts, and how From the Shadows' clone is logged.
