# Mara test inventory

No Mara tests exist in the Python sim (`fellowship_elarion_sim`) or the C++ tests (`fellowsimc`), so nothing was ported. The tests were written from the engine's code (`Heroes/Mara/`: the tooltips quoted in each ability and talent, `NOTES.md`), one behaviour per test, rules from the tooltips and values from the logs as the notes give them. They are to be reviewed against the game.

## Files

| File | Covers |
|---|---|
| `ResourceEconomy` | starting Energy and Combo Points, generator costs and gains, crit gains, the Combo Point cap, finisher costs, Energy regeneration (haste, Maiden of Death, Predator's Rush), bleed Energy |
| `CastTimes` | the 1s unhasted global cooldown of every ability, off-global abilities, a rotation |
| `AbilitiesSimple` | damage coefficients, Combo Point scaling, falloff, bleed duration and damage, Stealth and its three poisons, Maiden of Death |
| `AbilitiesComplex` | Spirit Refund, Seething Poison on the bleed, Final Stratagem, Matriarch Macabre and its clones, Macabre Stratagem |
| `TalentsSimple` | Efficient Killer, Venomous Delight, Sinner's Pride, Assassin's Guile, Feed the Queen, Malevolence, Arachnid Onslaught, Maiden's Doom, Puncture, Red Ledger, Bloodrush, Gushing Blood, Caustic Wounds, Seething Burst, Corrosive Spill, Hemotoxin |
| `TalentsComplex` | Deadly Scheme, Gushing Blood with Seething Poison, Creeping Death |
| `CooldownDurationSimple` | charges and cooldowns, haste on Widow's Bite, Brooding Shadows and the auto attack |
| `HasteBreakpoints` | tick counts of the bleed, Seething Poison and Volatile Poison |

There is no `CooldownDurationComplex` (no test yet for several reductions at once beyond Sinner's Pride).

## Topics left for `Specific/` (not tested; for review)

- **Shadowy clones' timing and shape:** the first clone strikes 0.335s after the cast and the second 0.335s after the first is done; Arachnid Assault clones hit one enemy per server frame; the clones keep the cast's bonuses (Malevolence, Assassin's Guile, Deadly Scheme's crit) but not Feed the Queen; Macabre Stratagem's extension by 10s; `From the Shadows` clones.
- **Volatile Poison's timeline:** lasts 6s (6 to 8 in the logs), the eruption 0.2s after the last tick, a re-application starting it again and cancelling the old ticks.
- **Hemorrhage refresh:** a refresh keeps the tick timing and carries over up to 30% of the new duration of the time left; partial tick on running out.
- **Deadly Scheme's stack accounting:** stacks past 40 carry over, the stacks keep counting while it is active, and one stack shows as the buff returns.
- **Spirit Refund of a finisher:** the Energy and Combo Points come back 0.2s after the spend; the clones do not roll it.
- **Vexira's Venom** (a legendary): a tick is a third of the venom held, ticks every 2s shortened by haste, restarts on a crit.
- **Hemotoxin's burst:** what "bleed damage" means (the bleed over the duration the Combo Points give), the nearby-enemy share and its falloff.
- **Energy regeneration against logs:** Venomous Delight at the cap, Energy regeneration changing as haste does (Spirit of Heroism).
- **Blocked hits:** the logged amount is what a block left (replay only).

## Not tested and why

- Auto attack damage (30% of Agility): the engine swings it only from an action list.
- Kick and Stalker Step (they keep Stealth): not in the engine.
- Stealth lasting 0.67s: this is the time the log showed before the next attack, not a rule.
