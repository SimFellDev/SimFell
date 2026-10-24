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
  with every bonus the cast had (Malevolence, Feed the Queen, Deadly Scheme's crit). Their hits are logged under the
  attack's own ID (clone/cast ratio 0.500 on 620 Arachnid Assault hits).
- **Volatile Poison:** each tick deals the 31.2% of Agility the tooltip gives for the whole poison; the eruption deals
  124.8%.
- **Caustic Poison:** every hit is a grievous crit (its +100% crit chance on top of Mara's).
- **Vexira's Venom:** a tick deals a third of the venom held; the shadowy clones' crits add none.
- **Backstab and auto attacks** from the front can be blocked (hit type 4: the logged amount is what the block left).

## Timing

- **Global cooldown:** 1s, not hasted (casts 1.032s apart at the least, during Spirit of Heroism too).
- **Auto attacks:** a 1.5s swing shortened by Haste (1.30s apart at 13% Haste, 1.035s during Spirit of Heroism); none
  in Stealth.
- **Stealth** (Brooding Shadows): ends with the next Backstab, Widow's Bite or Skittering Blades, 0.67s after it starts
  at the median; Kick and Stalker Step keep it.
- **Hemorrhage:** ticks every 3s shortened by Haste, with a partial tick as it runs out. A refresh keeps the tick timing,
  and up to 30% of the new duration of the time left carries over.
- **Seething Poison:** ticks every 2s shortened by Haste; its 60s are not hasted.
- **Volatile Poison:** lasts between 6 and 8 seconds (effect 1731 DurationMagnitude 6 to 8) and ticks 6 times raised
  by Haste, rounded (7 at 13% Haste, 9 during Spirit of Heroism), evenly spread; it erupts 0.2s after its last tick.
- **Shadowy clones:** the first strikes 0.335s after the cast, the second 0.335s after the first is done; a clone's
  Arachnid Assault hits one enemy per server frame.
- **Vexira's Venom:** ticks every 2s shortened by Haste, with a partial tick as it runs out; a new crit restarts its 6
  seconds and keeps the tick timing.

## Resources

- **Energy:** 200 at most, full at the start. It regenerates at 15 per second raised by Haste (17.3 per second with no
  buff at 13% Haste), times 1.2 with Predator's Rush and 1.2 with Maiden of Death.
- **Spirit Refund:** a finisher's refund gives back its Combo Points and its Energy (fight 19 @ 25.691: 6 Combo Points
  and 40 Energy), and 1 Spirit Point.
- **Deadly Scheme:** a stack per 5 Energy spent; the stacks past 40 carry over, and while it is active the stacks keep
  counting without the buff. The log shows 1 stack as the buff comes back, until the next stack.
- **Venomous Delight:** a proc is a 10 Energy jump right after a poison hit (172 jumps of 8 to 12 in fight 19). A proc
  at full Energy shows nothing.
- **Malevolence:** a finisher uses one stack of its own buff, then grants the other.

## Not yet checked against a log

Red Ledger, Corrosive Spill, Bloodrush, Efficient Killer, Gushing Blood, Maiden's Doom, Seething Burst, Hemotoxin,
Arachnid Onslaught, Caustic Wounds, Puncture, Final Stratagem and From the Shadows are built from their tooltips and the
game data (marked `UNVERIFIED` in their files). A log of a build with them would settle: the damage ID of Seething Burst,
what Hemotoxin's "bleed damage" counts, and how From the Shadows' clone is logged.
