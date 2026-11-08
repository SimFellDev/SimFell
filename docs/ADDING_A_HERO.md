# Adding a hero (and talents, gear and buffs)

This guide is for adding content to the engine by hand: a new hero, abilities, talents, aspects, sigils, protective
enchants, glyphs, relic passives, legendaries, weapons and buffs from other players. Rime (`src/FellowshipSim.Core/Heroes/Rime`) is the complete example
to copy from.

## How the engine is organised

```
src/FellowshipSim.Core/
  Domain/         Hero, Ability, Aura, resources, talents: the base classes everything builds on
  Engine/         the simulation: time, events, damage, periodic effects, procs, cooldowns
  Heroes/<Hero>/  one folder per hero: <Hero>.cs, Abilities/ (one file each), Talents/ (one file each)
  Customization/  gear shared by all heroes: Aspects/, Sigils/, ProtectiveEnchants/ (all Augmentations/), Glyphs/, Gems/,
                  RelicPassives/, Legendaries/, Weapons/, WeaponAbilities/
  Import/         CharacterBuild (a build by game IDs) and HeroImporter (build -> hero)
  Catalog.cs      finds every class above by its ID: nothing to register by hand. A talent's, augmentation's, glyph's
                  and relic passive's Id, a legendary's ItemIds and HeroName and a weapon's WeaponAbilityId are read
                  from an instance

src/FellowshipSim.Engine/
  Replay/         replaying combat logs and the review page, with everything only a replay needs (see section 5)
```

The engine knows nothing about combat logs or replays: `FellowshipSim.Core` does not reference the replay. What a
replay needs to read a log (how it reports resources, what proves a proc, buffs from other players, past balance
values) lives in tables in `FellowshipSim.Engine/Replay`.

Three rules hold everywhere:

1. **One class per file**, named after the class.
2. **Game values are public constants at the top of the class**: IDs, names, coefficients, cooldowns, durations,
   percentages, per-rank tables. Nothing is read from game data at runtime, so a patch that changes a number is a
   one-line edit.
3. **Write down where a value comes from** in its comment: the tooltip text, the game data record, or the combat logs it
   was measured from. Mark anything not yet checked against a log with `UNVERIFIED`.

## Where game values come from

- **The game data (datamine):** the Codex SDK exports `entities.jsonl` with every ability, effect, talent, aspect,
  sigil, protective enchant, glyph, gem ability and relic passive: cooldowns, cast times, coefficients, effect durations, stacks and stat modifiers. Use it as
  the reference when writing constants, and compare against it when the game is patched.
- **Combat logs:** what really happens (timings, procs, partial ticks). The replay review shows where the engine and a
  log disagree.

**IDs in combat logs** are game IDs plus an offset: abilities as-is, effects (auras and effect damage) + 1,000,000,
talents + 2,000,000, augmentations + 3,000,000 (see `Domain/LogIds.cs`). Glyphs are the exception: logs give them per-item
IDs, so they are matched by name.

## 1. The hero class

Create `Heroes/<Hero>/<Hero>.cs`, a `partial` subclass of `Hero`. The class name is the hero's name in Pascal case
(`Elarion`); the catalog finds it automatically, and the web page lets you replay that hero's fights once it exists. The
record is the hero of that name in the game data. `HeroGenerator` writes the class's game data half:

- `HeroId` and `HeroName`;
- one constant per scalar field of the record, named `Hero` and the field's name: `HeroTitle`, `HeroMainStat`,
  `HeroColor`, `HeroPortrait`, `HeroRole`, `HeroRoleIcon`, `HeroOverallDifficulty`, `HeroOffense`, `HeroDefense` and
  `HeroControl`;
- one constant per base attribute of the record, named `Hero` and the attribute's name: `HeroBalancingDamageAura`,
  `HeroMaxResources`, `HeroResources`, `HeroMaxResourcesSecond`, `HeroMaxMana`, `HeroManaRegenRate`, `HeroBaseHealth`...;
- one constant per tuning value of the record with no owner or the hero as its owner, under the tuning value's name.

A number is an `int` when it is whole and a `double` otherwise. The generator leaves out a tuning name with two different
values (warning CODEX032) and a name the class or a base class already has (warning CODEX033). The class's own part has
the behaviour and the values the record does not have: Focus and Energy regeneration per second, Spirit Points per Spirit
Refund, log timings and proc keys.

```csharp
public sealed partial class Elarion : Hero
{
    public static readonly ResourceDef Focus = new("Focus", Max: HeroMaxResources, Initial: HeroResources);

    public static readonly TalentSlot[] TalentTree = TalentSlot.Tree(
        TalentSlot.InRow(1, 2, /* talent IDs in columns a, b, c */ 0, 0, 0)
        /* ... one row per talent row in game */);

    public override IReadOnlyList<TalentSlot> TalentTreeSlots => TalentTree;

    public override MainStat MainStat => HeroMainStat;

    public override void Initialize()
    {
        AddResource(Focus);
        SpiritRefundResource = Focus;               // Spirit Refund gives back the hero's spender resource
        ClassDamageTuning = HeroBalancingDamageAura;  // every hero's class tuning aura (1 = none)
        Abilities.Add(new FocusedShot(this));
        // ...
    }

    public override void ResetForIteration() => ResetCombatState();
}
```

Shared mechanics are already in `Hero` and need no code: Spirit Refund (chance Spirit / (1 + Spirit), Rising Spirit,
the refund arriving 0.2s later), Spirit of Heroism, the global cooldown, cooldown recovery and haste, primary stat,
crit, expertise and spirit.

## 2. Abilities

One file per ability in `Heroes/<Hero>/Abilities/`. An ability with a record in the game data is a `partial` class named
after the record (the name in Pascal case, letters and digits only, an apostrophe inside its word) in the hero's
namespace. Of several records of that name and hero, the one with a category is the class's. `AbilityGenerator` writes
the class's game data half:

- `AbilityId` and `AbilityName`;
- one constant per scalar field of the record, named `Ability` and the field's name: `AbilityCategory`,
  `AbilityCooldown`, `AbilityCharges`, `AbilityGlobalCooldown`, `AbilityGlobalCooldownScalesWithHaste`,
  `AbilityCastTime`, `AbilityCastTimeScalesWithHaste`, `AbilityChannelTime`, `AbilityChannelTick`, `AbilityCost`,
  `AbilityResource`, `AbilityDuration`, `AbilityPeriod`, `AbilityRange`, `AbilityMinRange`, `AbilityRadius` and
  `AbilityMaxTargets` (range, minimum range and radius in centimetres);
- one constant per tuning value of the record, under the tuning value's name.

A number is an `int` when it is whole and a `double` otherwise. The generator leaves out a tuning name with two different
values (warning CODEX008) and a name the class or a base class already has (warning CODEX009). The class's own part has
the behaviour and the values the record does not have: log timings, proc keys, other records' effect IDs. Constants
first, then auras, then the constructor and what the ability does:

```csharp
/// <summary>Focused Shot: ... (tooltip text)</summary>
public partial class FocusedShot : ElarionAbility
{
    public const double FocusGain = 20;

    public FocusedShot(Elarion owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = AgilityCoefficient;
        CastTime = SimTime.FromSeconds(AbilityCastTime);
        // SetCooldown(seconds, charges, scalesWithHaste) and SetChannel(duration, tickPeriod) as the game data says.
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        Owner.GainResource(Elarion.Focus, FocusGain, ctx.CurrentTime);
        DealDamage(ctx, target);
    }
}
```

What the base class gives you:

| Need | Use |
|---|---|
| Cooldown, charges, haste-scaled cooldown | `SetCooldown(seconds, charges, scalesWithHaste)` in the constructor, `TriggerCooldown(ctx)` when cast |
| Channel | `SetChannel(duration, tickPeriod)`, then override `OnChannelTick` / `OnChannelEnd` |
| Usable during the global cooldown | `IgnoresGcd = true` |
| Can it be cast now (costs, requirements) | override `CanCast` |
| One hit | `DealDamage(ctx, target, damageMultiplier, canCrit, ..., onLanded, extraCritChance)` |
| Hits on every enemy, with sqrt(T/N) falloff beyond T targets | `AoeTargetThreshold = T`, then `DealAoeDamage(ctx, ctx.ActiveEnemies)` |
| Damage logged under an effect ID | `DamageLogId = LogIds.Effect(effectId)` |
| A hasted damage-over-time or pulsing effect | `PeriodicEffect.BeginHasted(...)` (handles haste changes and the partial final tick), with `damageLogId` and, for an effect on one enemy, `target`: an exact replay fires each tick at its log tick |
| A buff | `public static readonly AuraDef Buff = AuraDef.Effect(name, effectId, seconds, maxStacks, modifiers)`, then `Owner.Auras.Apply(Buff, ctx.CurrentTime)` (an aura built at runtime, e.g. from a rank, is passed to `hero.Register(aura)` so it is listed too) |
| Spending or gaining a resource | `Owner.SpendResource(resource, amount, ctx)`, `Owner.GainResource(resource, amount, ctx.CurrentTime)` |
| A chance to proc | `ctx.Outcomes.RollProc(ProcKey, chance, ctx)` with a `public const string ProcKey` naming the proc. A buff the proc applies has `with { IsProc = true }`: an action list sees its gain after the proc reaction time |
| Procs per minute | `new RealPpm(ProcKey, ppm).Roll(hero, ctx)` |
| Something later | `ctx.Schedule(SimTime.FromSeconds(seconds), c => ..., "label")` |

Action lists (APL) refer to abilities by their name in snake case (`focused_shot`), to resources by name
(`focus`, `resource.focus`) and to buffs as `buff.<name>.stack`.

## 3. Talents

One file per talent in `Heroes/<Hero>/Talents/`. A talent with a record in the game data is a `partial` class named
after the record (the name in Pascal case, letters and digits only, an apostrophe inside its word) in the hero's
namespace; the record is the talent of that name in one of the hero's talent slots. `TalentGenerator` writes the class's
game data half:

- `TalentId` and `TalentName`;
- one constant per scalar field of the record, named `Talent` and the field's name: `TalentCooldown`, `TalentCost`,
  `TalentDuration`, `TalentRange` and `TalentRadius`;
- one constant per tuning value of the record, under the tuning value's name.

As for abilities, a number is an `int` when it is whole and a `double` otherwise. The generator leaves out a tuning name
with two different values (warning CODEX012) and a name the class or a base class already has (warning CODEX013). The
class's own part has the `Name` and `Id` overrides, the behaviour and the values the record does not have. A talent hooks
its effect into the hero in `Bind`, so abilities never check which talents are taken:

```csharp
/// <summary>Talent 627: "Each pulse of Bursting Ice grants you Harrowing Ice ..." (tooltip text)</summary>
public partial class HarrowingIce : Talent
{
    public const int EffectId = 2839;
    public static readonly AuraDef Buff = AuraDef.Effect("Harrowing Ice", EffectId, Duration, MaxStacks);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var burstingIce = hero.GetAbility<BurstingIce>();
        burstingIce.PulseDamageMultipliers.Add(ctx => 1.0 + DamageScalerPerStack * hero.Auras.GetStacks(Buff, ctx.CurrentTime));
        burstingIce.Pulsed += ctx => hero.Auras.Apply(Buff, ctx.CurrentTime);
    }
}
```

Hooks every hero has:

| Hook | Fires when |
|---|---|
| `hero.OnCastStarted`, `hero.OnCastCompleted` | a cast starts / resolves (instants resolve at once) |
| `hero.OnChannelTick`, `hero.OnChannelEnded` | a channel ticks / ends or is stopped |
| `ability.Hit` | one of that ability's hits lands (target, damage, whether it crit) |
| `hero.OnDamageDealt` | any hit lands (with its damage and whether it crit) |
| `hero.OnCastResolving` | a cast is about to resolve: add to it ("your next X critically strikes"), see below |
| `hero.OnResourceGained`, `hero.OnResourceSpent`, `hero.OnResourceOverflow` | resource is gained (and from which enemy) / spent / gained past its maximum |
| `hero.AbilityDamageMultipliers`, `hero.TargetDamageMultipliers`, `hero.TargetCritChanceBonuses` | read on every hit |
| `hero.CooldownAccelerations` | time rates on one ability's cooldown, added outside the Cooldown Recovery attribute |
| `hero.CooldownRecoveryBonuses`, `hero.CooldownRecoveryCompounds` | calculated multiplier and MultiplyCompound sources of the Cooldown Recovery attribute; a fixed multiplier source is a `StatType.CooldownRecovery` aura modifier |
| `hero.PassiveAuras` | always-on auras (stat bonuses) |
| `hero.SituationalAuras` | auras that depend on what the engine does not simulate (your health, standing still, a boss fight): up all fight in a sim, following the log in a replay |
| `hero.OnSpiritRefund`, `hero.SpiritOfHeroismStarted`, `hero.SpiritOfHeroismEnded` | a Spirit Refund triggers / Spirit of Heroism starts or ends |
| `ability.BonusCoefficient`, `ability.CritPowerMultiplier`, `ability.CritChanceBonus` | extra Main Stat damage, crit power and crit chance on one ability's hits |
| `hero.GuaranteedSpiritRefunds` | effects that make the next Spirit Refund certain |

"Your next X critically strikes" and similar buffs use `hero.OnCastResolving`: check the ability, use up the buff and add
to the cast; every hit of that cast gets it (Rime: `FrostweaversWrath`, `IcyFlow`). A proc that casts an ability for
free resolves it as a cast too (`IceComet.FreeCast`), passing itself as the source.

An ability that reacts to another ability's hits subscribes to its `Hit` event in `Connect()`, which runs once all the
hero's abilities exist (Rime: Ice Blitz extending on every Frost Swallow hit).

When a talent needs a moment the general hooks do not cover, give the ability an event for it and raise it at that
moment (Rime: `BurstingIce.Pulsed`, `FrostBolt.Landed`, `IceComet.CometCast`), or a list the talent adds to
(`BurstingIce.PulseDamageMultipliers`, `GlacialBlast.CastHasteBonuses`). If a talent
changes how a spell works rather than adding to it (Icy Talons replacing two abilities), set a clearly named property
on the hero or ability from the talent's `Bind` and let the ability read it. An ability that replaces another and
counts as it for talents inherits from it and overrides only what differs (Rime: `TalonStrike : GlacialBlast`,
`RisingTalons : IceComet`); `hero.GetAbility<GlacialBlast>()` still returns Glacial Blast itself.

A talent that deals damage of its own keeps that inside the talent: a small private `Ability` subclass for the damage
numbers (coefficient, log ID, target falloff) and the talent's own state (Rime: `CoalescingFrost`).

Also list every talent in the hero's `TalentTree`, one `TalentSlot.InRow(row, cost, a, b, c)` per row as in game, and return it
from `TalentTreeSlots`. The action-list editor lists the auras of every talent in it.

## 4. Gear and other content

All found by the catalog through their ID constants; add the file and it is used.

| Content | Folder | Identified by |
|---|---|---|
| Aspect, sigil, protective enchant | `Customization/Aspects`, `Sigils`, `ProtectiveEnchants` | a `partial` class deriving from `Aspect`, `Sigil` or `ProtectiveEnchant`, named after the record of that type (Pascal case, letters and digits only, an apostrophe inside its word), with `Bind` and its `Id` and `Name` overrides; `AugmentationGenerator` writes its `<Type>Id` and `<Type>Name`, a constant per scalar field of the record (`<Type>MaxRank`, `<Type>RandomChance`, `<Type>Cooldown`, `<Type>Duration`, and `<Type>ProcsPerMinute` from its one proc roll), a constant per tuning value under the tuning value's name, and each rank run as `public static readonly double[] <Key>ByRank` (the run's key without `Trait.<Row>.`, its segments joined) read with `ForRank(...)`. A `delta` run's values are multipliers (1.072 for 7.2%), and a `ticks` run's values are per tick |
| Glyph | `Customization/Glyphs` | a `partial` class deriving from `Glyph`, named after the glyph record (Pascal case, letters and digits only, an apostrophe inside its word), with `Bind` and its `Id` and `Name` overrides; `GlyphGenerator` writes its `GlyphId` and `GlyphName` (logs match glyphs by name), `GlyphMaxRank` and `GlyphAbilityRank`, and each tuning value under its name: a constant when the value is the same at every rank, otherwise `public static readonly double[] <Name>ByRank` read with `ForRank(...)` |
| Gem colour | `Customization/Gems` | its `AllNodes` table (node ID, slot, required power, values) |
| Relic passive | `Customization/RelicPassives` | a `partial` class deriving from `RelicPassive`, named after the relic passive record (Pascal case, letters and digits only, an apostrophe inside its word), with `Bind` and its `Id`, `Name` and `Items` overrides (`Items` returns `RelicPassiveItems`); `RelicPassiveGenerator` writes its `RelicPassiveId` and `RelicPassiveName`, `RelicPassiveCooldown` and `RelicPassiveProcsPerMinute` (from its one proc roll), a constant per tuning value under the tuning value's name, each granting item's `<Slot>ItemId` and `<Slot>ItemName` (`RelicItemId`, `RelicItemName`), and `public static readonly RelicItem[] RelicPassiveItems` |
| Legendary | `Customization/Legendaries` | a `partial` class deriving from `Legendary`, named after the legendary power (Pascal case, letters and digits only), with `Bind`; `LegendaryGenerator` writes its `PowerId`, its items' `<Slot>ItemId`, a constant per tuning value under the tuning value's name, and its `LegendaryPowerId`, `Name`, `HeroName` and `ItemIds` |
| Weapon | `Customization/Weapons` (+ its ability in `WeaponAbilities`) | a `partial` class deriving from `Weapon`, named after the weapon power or an item granting it (Pascal case, letters and digits only, an apostrophe inside its word), with its `Name`, `Equip` and a `WeaponAbilityId` override returning `WeaponPowerAbilityId`; `WeaponGenerator` writes its `WeaponPowerId`, `WeaponPowerName` and `WeaponPowerAbilityId`. Its ability is a `partial` class deriving from `Ability` in `WeaponAbilities`, named after the weapon power; `WeaponGenerator` writes its `WeaponPowerId`, `WeaponPowerName`, the granted ability's `AbilityId`, `AbilityName` and fields as `Ability<Field>` (`AbilityCategory`, `AbilityCooldown`, `AbilityCastTime`...), and a constant per tuning value of the power under the tuning value's name |
| Gear items (the editor's slots and their stats) | `Import/GameGearData.cs`, written by `tools/generate-gear.cs`, not by hand | the item ID, per hero |
| Buff from another player | `FellowshipSim.Engine/Replay/PartyBuffs.cs` | the buff's effect ID (optionally scaled by the giver's sigil rank) |

Percentage bonuses to crit, haste, expertise or spirit go in `hero.Stats.Bonus*` (they apply in replays too: logs only give
ratings). Flat or percentage Main Stat bonuses apply only when `!hero.Stats.UsesSheetValues`, since a logged Main Stat
already includes them. A protective enchant derives from `ProtectiveEnchant`, which has no effect on damage.

## 5. Checking against combat logs

A replay needs a few facts about how logs show your hero, kept in `FellowshipSim.Engine/Replay` (the engine never sees
them):

| Table | What to add |
|---|---|
| `LogResources` | each resource the log reports: its resource type number, scale and tolerance |
| `ProcEvidence` | for each `ProcKey`: the aura or hit that proves in the log it procced |
| `ClassTuningHistory` | past class tuning values, so fights from before a change replay with the value they had |
| `PartyBuffs` | buffs other players give that change your damage |

Buffs to compare with the log are found by themselves (`ModeledAuras`: every `AuraDef` constant on the hero and what it
has).

1. Run the web page (`aspire run`), paste a log with your hero and click the player.
2. Open the review. It shows damage and hits per ability against the log, every step where the engine disagreed, casts
   the engine would have refused, and anything the build uses that is not modeled.
3. Fix the biggest differences first; a whole ability off by a constant factor usually means a missing talent, buff or
   multiplier, while scattered single hits are usually timing.
4. `dotnet test` replays every downloaded fight (`data/fights`) and checks each talent of Rime's tree in a normal sim.

`docs/REPLAY_ACCURACY.md` has the accuracy targets and the measure over many fights; `/replay-accuracy <Hero>` runs that
work for one hero in its own worktree.
