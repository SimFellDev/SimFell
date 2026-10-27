# Weapon abilities: tests and open questions

Written from the engine's code and the tooltips quoted in it, over every hero authorized to have the weapon. Authorization is a list per hero in `GearHero.WeaponAbilities` (`GearFights.cs`), with no
grouping of heroes (not by role, nor by main stat): Rime, Ardeos and Elarion have Chronoshift, Icicles of An'zhyr, Voidbringer's Touch and Nature's Fury; Gunde,
Mara and Tariq have Fated Strike, Sahril's Wrath and Voidbringer's Touch (Earthbreaker is missing from the engine). The engine itself binds any weapon to any hero.
The heroes come from those lists; each test is a theory over them. Damage is read as a multiple of Main Stat (1000) over the hero's class tuning (1.04 Rime and
Mara, 1.1 Ardeos, 1.08 Elarion, 1.0 Gunde and Tariq), so one coefficient serves every hero.

| File | Covers |
|---|---|
| `WeaponFights.cs` | the heroes per weapon (theory data) and helpers: a hero with a weapon, coefficient, cooldown in cooldown seconds, aura reads |
| `WeaponEquipping.cs` | each hero has its set; none without a weapon; weapon on cooldown after a cast; Sinister, Mystic and Trickster leave it alone; cooldown recovery (The Monarch, haste) does not speed it |
| `WeaponFatedStrike.cs` | 739%, cleave 287.4% with the sqrt(3/N) falloff, 60s cooldown, Glorious Purpose 6s and its tripled recovery of other cooldowns (not Fated Strike's own) |
| `WeaponSahrilsWrath.cs` | 2109%, sqrt(1/N) falloff, Sundering Wrath stacks (1 per enemy, 5 max, 6% crit each, 20s), 120s cooldown, castable during the global cooldown |
| `WeaponVoidbringersTouch.cs` | eruption at 15s, 10% stored (damage to any enemy, DoTs included), crit +100%, the cap at 4250%, eruption on low health, 90s cooldown, during the global cooldown |
| `WeaponChronoshift.cs` | cast 1s hasted, pulse 576.9%, falloff sqrt(5/N), pulse timing and partial final pulse, barrier duration, 8x recovery of other cooldowns, 180s cooldown |
| `WeaponIciclesOfAnzhyr.cs` | 3 waves at 0.27/1.27/2.27s, 144%, falloff sqrt(15/N), the curse (applied by the final wave, 38.4% ticks, hasted period), +200% from Icicles on cursed enemies, 40s cooldown |
| `WeaponNaturesFury.cs` | cast 1.5s hasted, 4 hits 0.3s apart, 1397.7% (first x2), +30% crit chance, 60s cooldown |

## Failing on purpose (possible engine bug)
- None. (Voidbringer's Touch's eruption scales with Expertise: confirmed by the user, `The_Eruption_At_The_Cap_Is_Scaled_By_Expertise` passes. The engine's code comment saying otherwise is stale.)

## Skipped, to check in game
- **Earthbreaker** is missing from the engine, so it is in no hero's list. `Earthbreaker_Is_A_Weapon_Ability_Of_Gunde_Mara_And_Tariq` (placeholder).

## Followed the engine, with a TODO in the test
- **Voidbringer's Touch applies the class tuning a second time:** the stored damage already carries it (1.04 Rime and Mara, 1.1 Ardeos, 1.08 Elarion), and the
  eruption runs through the damage calculation again. The logs' eruptions are 0.94-1.05 of the engine's (mean 0.99, Rime), which fits both. The tests expect
  10% x stored x 2 x tuning.
- **Fated Strike's cleave count** (sqrt(3/N)): N counts the other enemies, not the target, as the log with 26 others (sqrt(3/26)) shows. Check in game whether the target counts.
- **Chronoshift's 8x recovery of other cooldowns:** settled by the user: the barrier is +700% cooldown acceleration (8 times the speed); the tooltip's 800% is wrong. Not open.
- **Chronoshift's first pulse** comes a server frame (0.033 seconds) after the cast completes; tested within 0.05 seconds.
- **Timed auras last half a frame longer** than their duration (the engine's measured rule): Glorious Purpose and Sundering Wrath are checked at the duration plus 0.03 seconds.

## Not covered
- The barrier's 50% damage taken (no damage taken in the sim).
- Voidbringer's Touch recast on a running touch (the cooldown prevents it) and two eruptions coming 0.6-1.2 seconds earlier in the log than the cap allows (unverified).
- Icicles' curse snapshot per re-application (damage, crit chance and crit power of the last cast) and its crit behavior; Nature's Fury's choice of targets for each bounce (dice).
- Weapon-based aspects and sigils (Brave Machinations, Martial Initiative, Visions of Grandeur, Inspired Allegiance): in the aspects and sigils packages, which can use these weapons.
- Weapon power and the other items of the weapon tree.
