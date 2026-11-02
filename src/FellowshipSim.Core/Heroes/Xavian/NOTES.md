# Xavian: what the logs showed

How Xavian's mechanics were pinned down, beyond the tooltips. Each point is also a comment next to the code it shapes.
Measured on the 16 Xavian players of patch 0.5.0.0 in `data/fights` (15 fights from 2026-10-23 to 2026-10-31, and
XyGpcHjNaAzgk7mW fight 22) unless noted. Game values are from game data build 26136736.

## Damage

- **Damage IDs:** Sun Strike, Blinding Slash, Solar Blades, Omnistrike, Brilliant Flash, Shining Halo, Decree of the Sun,
  Sky Crash and Ruptured Dawn are logged under their ability IDs. Aura of Solace's pulses are effect 2669 (1002669),
  Sunburn 2660, Invictus 2721, Counter-Attack 2651 and Solaris 2553.
- **Counter-Attack:** Xavian's Parry of an enemy's attack triggers it on that enemy (talent Vanguard of Vengeance). A
  Hallowed Shield stack and a Vanguard of Vengeance stack are in the same server frame (XyGpcHjNaAzgk7mW fight 20 @
  7.516 and 15.834).
- **Solaris:** Xavian's overhealing triggers it on nearby enemies (GE_Sune_OverhealToAoeDamage). In XyGpcHjNaAzgk7mW
  fight 20 it is 0.5s after a Brilliant Flash (11.988 -> 12.492, 13.223 -> 13.730). A replay deals Solaris at the log's hits
  with the log's amounts (exact replay the matched hit, free dice the nearest log hit within 0.1s); a sim has none.
  Known gap over the 16 fights: the engine has no hit for 1,774 of the 74,327 log hits (exact replay 98.7% of the log's
  19.85M, free dice 98.8%), and 361 engine hits are below the log's amount (log 811 against engine 67 on 23.1 at
  677.597).
- **Solar Blades:** the main target takes its 200% whatever the count. Every other enemy loses damage as sqrt(5/N) over
  all N enemies struck, the main target counted (log/engine 0.99-1.01 at every N from 2 to 17, 27,127 hits).
- **Brilliant Flash, enhanced:** the main target takes 150% with no falloff. The other N enemies lose damage as
  sqrt(3/N) over those N alone; counting the main target too left them 1.14 times low at N = 3 and 1.05 at N = 12.
- **Sun Strike:** each nearby enemy's hit rolls its own crit at 15% of the main hit's power, losing damage as sqrt(3/N)
  over the N nearby enemies.
- **Invictus:** every enemy takes the same amount, never a crit, losing damage as sqrt(1/N) over N enemies. Its
  calculation preset (FlatDamageHealingScaling) applies Expertise once more after the absorb. Log/engine is 0.92-1.09
  a fight.
- **Shining Halo:** the pulses keep the damage of the cast's moment. A buff gained or lost after the cast leaves them as
  they are (XyGpcHjNaAzgk7mW fight 20 @ 15.099 and 39.519).
- **Sunburn (Solar Burn):** a tick deals a sixth of the main target's Solar Blades hit, crit included (2302 -> 384), and
  never crits or spreads. A refresh adds the damage left, the old tick times the time left over 1.5s, to the new hit
  (XyGpcHjNaAzgk7mW fight 20, 39.621-69.472: the old tick's share is 0.44-0.55 against 0.43-0.55 from the time left).
- **Attack:** 609 of 7,353 swings were avoided (19 misses and 11 dodges in XyGpcHjNaAzgk7mW fight 20). No other Xavian
  hit was avoided.

## Timing

- **Global cooldown:** 1.5s shortened by Haste (Brilliant Flash and Omnistrike 1.17s apart at the most common spacing).
- **Attack:** swings 2.20s apart at about 18% Haste with no Haste buff, 1.80s during Spirit of Heroism.
- **Shining Halo:** a pulse as it is cast, then one every 1.50s whatever the Haste. A new halo replaces the one up.
  Each Brilliant Flash while it is up adds 2s.
- **Aura of Solace:** a pulse every 1.50s from the cast whatever the Haste, 10 Mana each. A pulse burns the enemies in
  combat with Xavian within a large radius (effect 2667), so the log has no pulse while none is near. In 4 fights with
  one cast the pulses end while effect 2667 stays up: Gift of Solace (effect 2670) leaves every ally at once and the log
  has no removal of 2667 (XyGpcHjNaAzgk7mW fight 20 @ 228.508 of 743s, fight 22 @ 53.203 of 722s, YC861NTRgLbmw4Qa
  fight 31 after 60.6s of 1079s, Fc8v6yWxD14GQMnV fight 19 after 105.3s of 1613s). The radius is 1500 around Xavian
  (ability 1915), and the pulses move with Xavian: every logged pulse hit is within 1600 of Xavian and 3400 or more from
  the cast. After each of the 4 ends, Xavian's melee hits reach enemies within 1400 of Xavian. Game data has no end
  condition for effect 2667 besides death (EffectLogic.RemoveOnDeath). A replay therefore deals each pulse at its group
  of log hits, with the engine's damage; a sim pulses every 1.50s while 2667 is up.
- **Sunburn:** a tick every 1.5s shortened by Haste, with a partial tick as it runs out (7 ticks of 593 and one of 342
  over 9.02s, XyGpcHjNaAzgk7mW fight 20 @ 74.748). A refresh keeps the tick timing.
- **Sky Crash:** the crash deals its damage 0.17s after the cast.

## Procs and resources

- **Swift Reprieval:** an aura with stacks (effect 2663). Brilliant Flash takes a stack, or an Omega Reprieval stack
  first when Xavian has one.
- **Golden Hour:** the Omnistrike after it removes Golden Hour and applies 1 stack of Omega Reprieval (XyGpcHjNaAzgk7mW
  fight 20 @ 29.724).
- **Solar Glare:** every enemy Solar Blades hits gets Blinding Horizon (effect 2727).
- **Mana:** the log reports Mana as resource type 1, in hundredths of a maximum of 1440.
- **Hallowed Shield and Vanguard of Vengeance:** their stacks come from Parries, and a replay follows the log's stacks.
