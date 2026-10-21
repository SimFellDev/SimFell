# Rime: what the logs showed

How Rime's mechanics were pinned down, beyond the tooltips. Each point is also a comment next to the code it shapes.
Measured on 10 Rime logs (fights on Wyrmheart and Apostate Veras, July to October 2026) unless noted.

## Damage

- **Class tuning:** +4% damage (hero attribute `BalancingDamageAura` 1.04) since 2026-08-27; replays of fights before
  then run without it (`ClassTuningHistory` in the replay). It applies to the weapon ability too: Chronoshift matches the
  logs with it after that date and without it before.
- **Target falloff** (damage per target beyond T targets is sqrt(T/N)): Bursting Ice 5, Rising Talons 12, Ice Comet 12,
  Glacial Assault's explosion 12, Chronoshift 5, The Vehement 5.
- **Rising Talons:** one wave of spikes per Winter Orb; the first 0.30s after the cast, then every 4 server frames.
  Every wave keeps the damage and target falloff of the cast, but hits the enemies present when it erupts.
- **Glacial Assault's explosion:** 30% of the hit actually dealt (crit included); it never crits itself.
- **Burstbolter:** Bursting Ice deals 1.3x (the game data's constant), not the tooltip's 20%. The pulse comes when the
  Frost Bolt lands, not when it is cast.
- **Harrowing Ice** (fight g6zBdpvJCmh2HfrD #31): a pulse deals damage with the stacks held before it, then adds its own;
  stacks reach 31 (the tooltip says 30) and are consumed at once.

- **Grievous crits:** crit chance above 100% gives base x (2 + overflow) x (1 + crit power).
- **Soulfrost Torrent (possible game bug):** its "+100% crit chance" counts twice on the Torrent it empowers, acting as
  +200% (target dummy at 30% crit: Soulfrost ticks 1.70x a normal crit, against 1.65 for +200% and 1.15 for +100%).
  Frostweaver's Wrath and Voidbringer's Touch count their +100% once.

## Timing

- **Bursting Ice:** pulses are hasted and re-time the moment haste changes. The debuff ends 1/60s after its 3s, with a
  partial final pulse worth the time left, only when at least 2 server frames have passed since the last pulse
  (`PeriodicEffect.MinHastedPartialTickSeconds`).
- **Freezing Torrent:** a tick as the channel starts, then every hasted period, plus a partial final tick; ticks come
  one server frame after the channel starts.
- **Wrath of Winter:** the first Winter Orb comes on activation; then one every 4s hasted by the haste when the previous
  orb came. Usable during the global cooldown.
- **Ice Blitz:** not hasted.
- **Cold Snap:** its charges recharge with haste added to the recovery rate ("Cooldown Acceleration equal to your
  Haste").
- **Chronoshift:** its own 8x cooldown recovery while channeling applies to Chronoshift too; weapon abilities ignore
  other cooldown recovery (Strand of Eternity, The Monarch).
- **Cancelling a cast** frees the global cooldown it started (all 46 instants cast right after a cancelled Frost Bolt).

## Resources

- **Spirit Refund:** chance Spirit / (1 + Spirit); Rising Spirit comes with the spend and the orbs 0.2s later; each
  further refund adds 2s to Rising Spirit instead of restarting it.
- **Frost Swallows:** every Winter Orb earned sends 3 swallows at enemies (game data Rime.AutoDamageProjectile.NumProjectiles). Flight of the Navir's 5
  circling swallows come back when they hit, so a Cold Snap right after a Freezing Torrent tick may send only 4.
- **Wisdom of the North:** 0.3s per orb spent and per orb that overflows. A free Glacial Assault Talon Strike counts
  nothing: it changes no Winter Orbs (game data; per the user, pending an in-game test).

## Not yet checked against a log

Avalanche, Cold Shower, Coalescing Frost, Icy Flow, Frostweaver's Wrath, Soulfrost Torrent and Supreme Torrent are built
from their tooltips and the game data (marked `UNVERIFIED` in their files). A log of a build using them would settle:
the damage ID of free Ice Comets, Icy Flow's crit bonus (+20% in the talent, +25% on the buff), and when Frostweaver's
Wrath rolls.
