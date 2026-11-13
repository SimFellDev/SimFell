using FellowshipSim.Core.Customization.Gems;

namespace FellowshipSim.Core.Dungeons.Affixes;

/// <summary>
/// Dungeon affix 24: "Three Anomalous Orbs spawn near enemies periodically. [...] the orbs cast Detonate, an interruptable
/// cast [...] Anomalous Orbs take 400% increased damage while not actively casting Detonate. [...] Each Anomalous Orb players
/// destroy grants +480 Gem Power to the corresponding color of the Orb that was destroyed".
/// <list type="bullet">
/// <item>An orb is an enemy named after creature 286 (<see cref="AnomalousOrbGameCreature"/>): a priority target with its
/// health and 0 Spirit Points. Each enemy line of the text with its name is an orb. With no such line, the fight spawns the
/// orbs by the game's rule (<see cref="ModifierTimer"/>): a spawn every <see cref="Constants.SpawnTimeMin"/> to
/// <see cref="Constants.SpawnTimeMax"/> seconds, uniform, the first from the run's start and each later one of combat
/// (Blueprint GA_DungeonModifier_Passive_AnomalousOrbs, ability 1949: WaitForModifierTrigger at ubergraph 1373, resumed
/// on combat begin at 6924, paused on combat end at 6997; the pause at 1638 comes before the first wait starts, binary
/// 0x145121B5A), of <see cref="Constants.NumOfOrbs"/> orbs, each after its own 0 to
/// <see cref="Constants.OrbRandomMaxDelayBetweenIndividualOrbSpawn"/> seconds (offset 3447). Out of a boss fight a spawn
/// needs an enemy in combat that is neither a boss nor an affix enemy, and is skipped without one (offsets 1847, 2879,
/// 3076); the next wait is a new draw. Each rule orb is in combat <see cref="RuleOrbSeconds"/>.</item>
/// <item>An orb casts Detonate (<see cref="DetonateCastTime"/> s) from entering combat (creature 286's
/// <see cref="DetonateInitialCooldown"/>; AIBC_Base sets AIPhase.DefaultCombat at its first threat). Logs: the first
/// begincast follows the orb's first event by 0.10 s (median, 120 orbs, the 6 downloaded fights with the affix). A cast
/// that completes kills the orb (K2_OnEndAbility, Auth_KillCharacter at offset 1300), with no Residue: the kill's
/// health event has the orb as its source (binary 0x1452E5387), and GrantBuffOnDeath needs another (offsets 254-301).</item>
/// <item>The party stops each cast: the first <see cref="PartyFirstStopSeconds"/> after it begins, each recast
/// <see cref="PartyRecastStopSeconds"/> after it begins, and the orb recasts <see cref="DetonateCcCooldown"/> later. An
/// interrupt of the hero's stops a cast as well, and the orb recasts <see cref="DetonateInterruptCooldown"/> later. Logs:
/// recasts after a stop come 9.02 s later (Ab1yCxVcNBHPz6t8 fight 7, 22 recasts; AFan1VtPvjYW3y7K fight 4, 6) and 12.02 s
/// after an interrupt (7w6mAjpTVgD14aHQ fight 11, VP9bnawhd6prjfKN fight 6). The 2 s of Global.AbilityPadding.Interrupt
/// and Global.AbilityPadding.CC run beside the recast, not after it (binary 0x145148840, a cooldown effect of its own).</item>
/// <item>An orb takes <see cref="AnomalousInstabilityIncomingDamageMultiplier"/> times the damage while it is not casting
/// (effect 2711, IgnoreTags CharacterStatus.Internal.Casting). A hit of an effect whose calculation preset has
/// ApplyTargetIncomingDamageHealingMultipliers off takes none of it (<see cref="Hero.DamageMultiplierAgainst"/>).</item>
/// <item>An orb's death, other than at its cast's end, gives the hero a stack of Anomalous Residue of the orb's gem colour,
/// drawn uniformly from the six (offset 6110; ability 1902 GrantBuffOnDeath): <see cref="Constants.AnomalousResidueAdditionalGemPowerPerStack"/>
/// power of that colour a stack, at most <see cref="Constants.AnomalousResidueMaxStacks"/>, for
/// <see cref="Constants.AnomalousResidueDuration"/> s from its last stack. Logs: 26 of the 27 orb deaths in
/// Ab1yCxVcNBHPz6t8 fight 7 and all 24 in CwmX2Tb8GA6hyc1v fight 8 give Gunde a stack within 0.1 s, each lasting 60.0 s
/// (median).</item>
/// </list>
/// </summary>
public sealed partial class AnomalousOrbs : DungeonAffix
{
    public override int Id => AffixId;
    public override string Name => AffixName;

    /// <summary>The orbs' name in a dungeon's text: creature 286's.</summary>
    public const string OrbName = AnomalousOrbGameCreature.NAME;

    /// <summary>
    /// The seconds a rule orb is in combat. Party behaviour, from the logs: the median orb life is 13.2 s in
    /// VP9bnawhd6prjfKN fight 6 (24 orbs) and 13.0 s in 8vb2pTAhFQRcktaD fight 2 (24 orbs).
    /// </summary>
    public const double RuleOrbSeconds = 13.0;

    /// <summary>
    /// The seconds from an orb's first Detonate to the party's stop. Party behaviour, from the logs: the median is 2.14 s in
    /// Ab1yCxVcNBHPz6t8 fight 7 (27 casts) and 2.17 s in 7w6mAjpTVgD14aHQ fight 11 (9 casts).
    /// </summary>
    public const double PartyFirstStopSeconds = 2.15;

    /// <summary>
    /// The seconds from an orb's later Detonate to the party's stop. Party behaviour, from the logs: the median is 2.21 s in
    /// 8vb2pTAhFQRcktaD fight 2 (19 casts) and 2.07 s in AFan1VtPvjYW3y7K fight 4 (6 casts).
    /// </summary>
    public const double PartyRecastStopSeconds = 2.15;

    /// <summary>Anomalous Instability (effect 2711) on an orb while it is not casting.</summary>
    public static readonly AuraDef Instability = AuraDef.Effect("Anomalous Instability", AnomalousInstabilityEffectId,
        modifiers: StatModifier.More(StatType.IncomingDamageMultiplier, AnomalousInstabilityIncomingDamageMultiplier));

    /// <summary>Anomalous Residue of each gem colour on the hero, by colour (effects 2704 to 2708 and 2710).</summary>
    public static readonly IReadOnlyDictionary<GemColor, AuraDef> Residue = new Dictionary<GemColor, AuraDef>
    {
        [GemColor.Topaz] = ResidueOf("Topaz", AnomalousResidueTopazEffectId),
        [GemColor.Sapphire] = ResidueOf("Sapphire", AnomalousResidueSapphireEffectId),
        [GemColor.Ruby] = ResidueOf("Ruby", AnomalousResidueRubyEffectId),
        [GemColor.Emerald] = ResidueOf("Emerald", AnomalousResidueEmeraldEffectId),
        [GemColor.Diamond] = ResidueOf("Diamond", AnomalousResidueDiamondEffectId),
        [GemColor.Amethyst] = ResidueOf("Amethyst", AnomalousResidueAmethystEffectId),
    };

    private static AuraDef ResidueOf(string colour, int effectId) =>
        AuraDef.Effect($"Anomalous Residue: {colour}", effectId, Constants.AnomalousResidueDuration, Constants.AnomalousResidueMaxStacks);

    private static readonly GemColor[] Colours = [GemColor.Amethyst, GemColor.Diamond, GemColor.Emerald, GemColor.Ruby, GemColor.Sapphire, GemColor.Topaz];

    /// <summary>
    /// The hero's <paramref name="colour"/> gem power with <paramref name="stacks"/> stacks of Residue: its bound power and
    /// the stacks' power, which the hero's gem power multipliers scale (they multiply after the Additive sum: Covetous Greed,
    /// effect 3271, MultiplyCompound). A colour with no power gets its gem with its first stack.
    /// </summary>
    public static void SetResidue(Hero hero, GemColor colour, int stacks, SimTime now)
    {
        if (hero.Gems.FirstOrDefault(g => g.Color == colour) is not { } gem)
        {
            if (stacks == 0) return;
            gem = Gem.Create(colour);
            hero.Gems.Add(gem);
            gem.Bind(hero);
        }
        double scale = hero.RelicPassives.Aggregate(1.0, (m, r) => m * r.GemPowerMultiplier);
        gem.ChangePower(gem.BasePower + Constants.AnomalousResidueAdditionalGemPowerPerStack * stacks * scale, now);
    }

    internal override void Begin(AffixRun run)
    {
        var ctx = run.Context;
        var hero = run.Hero;
        // The hero's gem power follows the Residue stacks, as they come and as they run out.
        void Follow(Aura aura, SimTime now)
        {
            foreach (var (colour, def) in Residue)
            {
                if (aura.Def == def) SetResidue(hero, colour, hero.Auras.GetStacks(def, now), now);
            }
        }
        void Gained(Aura aura, int _, SimTime now) => Follow(aura, now);
        hero.Auras.OnGained += Gained;
        hero.Auras.OnRemoved += Follow;
        run.Ended += c =>
        {
            hero.Auras.OnGained -= Gained;
            hero.Auras.OnRemoved -= Follow;
            foreach (var (colour, def) in Residue)
            {
                hero.Auras.Remove(def, c.CurrentTime);
                SetResidue(hero, colour, 0, c.CurrentTime);
            }
        };

        var orbs = new Dictionary<EnemyTarget, Orb>(ReferenceEqualityComparer.Instance);
        run.EnemyEntered += (c, enemy) =>
        {
            if (enemy.Name != OrbName || orbs.ContainsKey(enemy)) return;
            enemy.IsPriority = true;
            orbs[enemy] = new Orb(enemy, Colours[Math.Min(Colours.Length - 1, (int)c.Outcomes.RollDelay(Colours.Length, c))], c);
        };
        ctx.EnemyDemising += (c, enemy) =>
        {
            if (!orbs.Remove(enemy, out var orb)) return;
            orb.End(c);
            if (!orb.Detonated) hero.Auras.Apply(Residue[orb.Colour], c.CurrentTime);
        };

        if (run.Dungeon.Pulls.Any(p => p.Groups.Any(g => g.Name == OrbName))) return;
        _ = new ModifierTimer(run, Constants.SpawnTimeMin, Constants.SpawnTimeMax, paused: true, c => SpawnWave(run, c));
    }

    /// <summary>One orb in combat: its gem colour, its Detonate casts and the party's stops.</summary>
    private sealed class Orb
    {
        public readonly EnemyTarget Enemy;
        public readonly GemColor Colour;
        public bool Detonated;
        private ScheduledEvent? _next, _stop;
        private bool _cast;

        public Orb(EnemyTarget enemy, GemColor colour, SimulationContext ctx)
        {
            Enemy = enemy;
            Colour = colour;
            enemy.Interrupted += (c, _) => Stopped(c, DetonateInterruptCooldown);
            enemy.CastChanged += (c, e) =>
            {
                if (e.IsCasting) e.Auras.Remove(Instability, c.CurrentTime);
                else e.Auras.Apply(Instability, c.CurrentTime);
            };
            enemy.Auras.Apply(Instability, ctx.CurrentTime);
            _next = ctx.Schedule(SimTime.FromSeconds(DetonateInitialCooldown), Cast, "Detonate");
        }

        private void Cast(SimulationContext ctx)
        {
            _next = null;
            if (Enemy.IsSleeping) return;
            double stop = _cast ? PartyRecastStopSeconds : PartyFirstStopSeconds;
            _cast = true;
            Enemy.BeginCast(ctx, DetonateAbilityName, DetonateCastTime, c =>
            {
                // A completed Detonate kills the orb.
                _stop?.Cancel();
                Detonated = true;
                c.RemoveEnemy(Enemy, defeated: true, died: true);
            });
            _stop = ctx.Schedule(SimTime.FromSeconds(stop), c =>
            {
                _stop = null;
                if (Enemy.StopCastByParty(c)) Stopped(c, DetonateCcCooldown);
            }, "Detonate stopped");
        }

        private void Stopped(SimulationContext ctx, double recast)
        {
            _stop?.Cancel();
            _stop = null;
            _next?.Cancel();
            _next = ctx.Schedule(SimTime.FromSeconds(recast), Cast, "Detonate");
        }

        public void End(SimulationContext ctx)
        {
            _next?.Cancel();
            _stop?.Cancel();
            Enemy.StopCast();
            Enemy.Auras.Remove(Instability, ctx.CurrentTime);
        }
    }

    /// <summary>The name of the orbs' cast.</summary>
    public const string DetonateAbilityName = "Detonate";

    /// <summary>
    /// A wave of the game's rule: out of a boss fight it needs an enemy in combat that is neither a boss nor an affix enemy,
    /// and each orb comes after its own delay.
    /// </summary>
    private static void SpawnWave(AffixRun run, SimulationContext ctx)
    {
        if (!run.BossFight && !ctx.ActiveEnemies.Any(e => !e.IsBoss && e.Name != OrbName)) return;
        for (int i = 0; i < Constants.NumOfOrbs; i++)
        {
            double delay = ctx.Outcomes.RollDelay(Constants.OrbRandomMaxDelayBetweenIndividualOrbSpawn, ctx);
            ctx.Schedule(SimTime.FromSeconds(delay),
                _ => run.Spawn(OrbName, AnomalousOrbGameCreature.BASE_HEALTH, RuleOrbSeconds, priority: true, AnomalousOrbGameCreature.SPIRIT_POINTS),
                "Anomalous Orb");
        }
    }
}
