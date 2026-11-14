using FellowshipSim.Core.Heroes.Gunde.Abilities;
using FellowshipSim.Core.Heroes.Gunde.Talents;

namespace FellowshipSim.Core.Heroes.Gunde;

/// <summary>
/// Gunde, the Northlander. His abilities deal damage that builds up Rend on their targets (<see cref="Rend"/>); Heart
/// Splitter's Exsanguinate and Slaughter turn the Rend into damage of their own. Gunde has no resource of his own:
/// combat logs report none for him. Abilities live in Abilities/, talents in Talents/.
/// </summary>
public sealed partial class Gunde : Hero
{
    /// <summary>
    /// Spirit Refund (hero 9's Spirit text): "whenever you use an ability that applies Rend, causing Blood Feathers to be
    /// created on the ground near you", Rising Spirit, and a Spirit Point (game data ability 1522).
    /// Combat logs (Y4wpb2f3gc8LqJDX fight 25 @ 24.978): Rising Spirit comes as the
    /// ability's first hit lands.
    /// </summary>
    public const int FeathersPerSpiritRefund = 5;

    /// <summary>
    /// "Each time Rend deals damage there is a chance a Blood Feather is spawned" (ability 2298); the game data has no
    /// chance. UNVERIFIED estimate from Y4wpb2f3gc8LqJDX fight 25: its Owed in Blood gains, less the feathers of Spirit
    /// Refunds, divided by its Rend ticks, less the feathers of enemies that died.
    /// </summary>
    public const double FeatherChancePerRendTick = 0.15;

    /// <summary>"Enemies that die with your Rend active on them have a chance to create a Blood Feather" (ability 2298).</summary>
    public const double FeatherChanceOnDeath = 0.25;

    /// <summary>
    /// A sim picks up every Blood Feather this long after it appears (combat logs, fight 25 @ 24.978: the feathers of a
    /// Spirit Refund come in as Owed in Blood shortly after). UNVERIFIED: pickups depend on where Gunde moves.
    /// </summary>
    public const double FeatherPickupSeconds = 0.5;

    public const string FeatherProcKey = "blood_feather";

    /// <summary>Gunde's talent tree, as in game (game data talent-slot records, hero Gunde).</summary>
    public static readonly TalentSlot[] TalentTree = TalentSlot.Tree(
        //              row cost  a                           b                            c
        TalentSlot.InRow(1, 2,    DeathsArc.TalentId,         RavensPrecision.TalentId,    GrimHarvest.TalentId),
        TalentSlot.InRow(2, 1,    SunderedFlesh.TalentId,     CrimsonStrikes.TalentId,     DeepRend.TalentId),
        TalentSlot.InRow(3, 2,    DarkeningHearts.TalentId,   MurderOfCrows.TalentId,      Massacre.TalentId),
        TalentSlot.InRow(4, 1,    SlayersGrin.TalentId,       FrenziedReign.TalentId,      Bloodbath.TalentId),
        TalentSlot.InRow(5, 3,    Bloodcraze.TalentId,        Oathshatter.TalentId,        Carnage.TalentId),
        TalentSlot.InRow(6, 1,    SuperiorSerration.TalentId, AncestralInstinct.TalentId,  HarvestersToll.TalentId));

    public override IReadOnlyList<TalentSlot> TalentTreeSlots => TalentTree;

    public override MainStat MainStat => HeroMainStat;

    /// <summary>Every enemy's Rend.</summary>
    public Rend Rend { get; }

    /// <summary>Share of a hit's damage Serrated Edge adds to its Rend (Superior Serration adds to it).</summary>
    public double SerratedEdgeShare { get; set; } = BloodArc.SerratedEdgeShare;

    /// <summary>Shares of a hit's damage added to its Rend while they hold (Reign in Blood, Frenzied Reign).</summary>
    public List<Func<SimTime, double>> RendShareBonuses { get; } = new();

    /// <summary>Multipliers on every hit of a cast that applies Rend, read and used up as the cast resolves (Crimson Strikes).</summary>
    public List<Func<SimulationContext, GundeAbility, double>> RendCastMultipliers { get; } = new();

    /// <summary>A pulse of hits that applies Rend went off (a cast's hits, or one spin of Grim Carve).</summary>
    public event Action<SimulationContext, GundeAbility>? RendPulse;

    internal void RaiseRendPulse(SimulationContext ctx, GundeAbility ability) => RendPulse?.Invoke(ctx, ability);

    /// <summary>A Blood Feather was picked up: Owed in Blood gained a stack, or refreshed at its maximum (Slayer's Grin, Raven's Precision).</summary>
    public event Action<SimulationContext>? FeatherCollected;

    public Gunde() => Rend = new Rend(this);

    public override void Initialize()
    {
        ClassDamageTuning = HeroBalancingDamageAura;
        Stats.BaseHealth = HeroBaseHealth;
        Stats.StaminaToMaxHealth = HeroStaminaToMaxHealthMultiplier;
        // Owed in Blood's stacks come from feathers Gunde walks over: a replay follows the log's stacks.
        SituationalAuras.Add(OwedInBlood.Buff);

        Abilities.Add(new Attack(this));
        Abilities.Add(new DoubleStrike(this));
        Abilities.Add(new BloodArc(this));
        Abilities.Add(new ReaversEdge(this));
        Abilities.Add(new HeartSplitter(this));
        Abilities.Add(new GrimCarve(this));
        Abilities.Add(new Rupture(this));
        Abilities.Add(new Slaughter(this));
        Abilities.Add(new ReignInBlood(this));
        Abilities.Add(new BloodboundSpirit(this));
        Abilities.Add(new OwedInBlood(this));
        Abilities.Add(new Warbound(this));
        Abilities.Add(new ButchersHook(this));
        Abilities.Add(new Jawbreaker(this));
        Abilities.Add(new RecklessAbandon(this));

        Rend.Ticked += (ctx, _) =>
        {
            if (ctx.Outcomes.RollProc(FeatherProcKey, FeatherChancePerRendTick, ctx)) CollectFeathers(ctx, 1);
        };
        Rend.EnemyDiedWithRend += (ctx, _) =>
        {
            if (ctx.Outcomes.RollProc(FeatherProcKey, FeatherChanceOnDeath, ctx)) CollectFeathers(ctx, 1);
        };
        // Every gain of Owed in Blood is a feather picked up, the log's ones in a replay too.
        Auras.OnGained += (aura, _, now) =>
        {
            if (aura.Def == OwedInBlood.Buff && SimContext is { } ctx && now > SimTime.Zero) FeatherCollected?.Invoke(ctx);
        };
    }

    /// <summary>
    /// The first hit of a cast that applies Rend landed: the cast rolls its Spirit Refund, and a refund creates
    /// <see cref="FeathersPerSpiritRefund"/> Blood Feathers.
    /// </summary>
    internal void RendCastLanded(SimulationContext ctx)
    {
        if (!TriggerSpiritRefund(null, 0.0, ctx)) return;
        for (int i = 0; i < FeathersPerSpiritRefund; i++)
        {
            if (ctx.Outcomes.RollProc(FeatherProcKey, 1.0, ctx)) CollectFeathers(ctx, 1);
        }
    }

    /// <summary>Picks up <paramref name="count"/> Blood Feathers a moment later: each is a stack of Owed in Blood.</summary>
    public void CollectFeathers(SimulationContext ctx, int count) =>
        ctx.Schedule(SimTime.FromSeconds(FeatherPickupSeconds), c =>
        {
            for (int i = 0; i < count; i++) Auras.Apply(OwedInBlood.Buff, c.CurrentTime);
        }, "Blood Feather");

    /// <summary>
    /// Sundered Flesh: after a tick of Rend or Slaughter, an enemy with less health than the damage the bleed holds takes
    /// its health at once.
    /// </summary>
    internal Action<SimulationContext, EnemyTarget, double>? SunderedFleshCheck { get; set; }

    /// <summary>Runs Sundered Flesh's check after a bleed's tick: <paramref name="held"/> is the damage the bleed still holds.</summary>
    internal void CheckSunderedFlesh(SimulationContext ctx, EnemyTarget target, double held) => SunderedFleshCheck?.Invoke(ctx, target, held);

    public override void ResetForIteration()
    {
        ResetCombatState();
        Rend.Reset();
    }
}
