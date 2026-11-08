using FellowshipSim.Core.Heroes.Mara.Abilities;
using FellowshipSim.Core.Heroes.Mara.Talents;

namespace FellowshipSim.Core.Heroes.Mara;

/// <summary>
/// Mara, the Hungering Blade. Her generators build Combo Points, and her finishers spend them. Every ability but Widow's Bite
/// costs Energy, and Energy regenerates. Backstab, Widow's Bite and Skittering Blades used from Stealth (Brooding Shadows)
/// apply a poison. Abilities live in Abilities/, talents in Talents/.
/// </summary>
public sealed partial class Mara : Hero
{
    /// <summary>
    /// Energy regenerated per second before haste and buffs: the game data gain amount every gain interval,
    /// the interval divided by Haste and by Predator's Rush, and Maiden of Death
    /// multiplies the amount with Maiden of Death.
    /// Combat logs (23 Mara fights of data/fights): the logged rate matches this rate in the four buff states.
    /// </summary>
    public const double EnergyPerSecond = 15.0;

    /// <summary>
    /// Seconds between two Energy regeneration ticks of the engine. A cast and a replay's check read the Energy regenerated up
    /// to their moment (<see cref="RegenerateEnergy"/>); the ticks let an action list waiting for Energy act again. A shorter period
    /// costs sim speed and changes no DPS.
    /// </summary>
    public const double EnergyTickSeconds = 0.1;

    /// <summary>
    /// Regeneration a restart of the tick timer loses. In game data a change of Haste or
    /// of Predator's Rush ends the tick timer and starts a new one after the delay until the next tick,
    /// with no tick at start, so one frame and the old timer's remainder are lost, and a second change in that frame finds no
    /// watcher. Maiden of Death scales the amount with no restart. The size of the remainder and
    /// the order of the timer and latent ticks in a frame are not in game data, so the size is a log pattern: over the 8 players with
    /// no Venomous Delight in the 23 Mara fights of data/fights (T2qLPpmMkrAtJ9B1 fight 18, 3M2vWr8taxVfTQFY fight 53 and NOTES.md),
    /// the log's Energy falls behind the engine's at each Haste drop, Haste rise and Predator's Rush change, by a short regeneration time.
    /// </summary>
    public const double EnergyLostToTimerRestartSeconds = PeriodicEffect.ServerFrameSeconds;

    /// <summary>Mara starts every fight with full Energy: <see cref="HeroMaxResources"/> and <see cref="HeroResources"/>.</summary>
    public static readonly ResourceDef Energy = new("Energy", Max: HeroMaxResources, Initial: HeroResources);

    /// <summary>The most Combo Points are <see cref="HeroMaxResourcesSecond"/>.</summary>
    public static readonly ResourceDef ComboPoints = new("Combo Points", Max: HeroMaxResourcesSecond)
    {
        Aliases = ["cp"],
    };

    /// <summary>
    /// Combat logs (Fc8v6yWxD14GQMnV fight 19, 999 casts in a row): casts on the global cooldown come the same time apart at the
    /// least, with Spirit of Heroism too. Mara's global cooldown is not hasted.
    /// </summary>
    public const double GlobalCooldown = 1.0;

    /// <summary>
    /// Creeping Death: Caustic Poison, Seething Poisons, and Volatile Poison deal increased Poison damage per Haste;
    /// Hemorrhaging Strike's bleed, Corrosive Spill and Seething Burst too (their tooltips).
    /// </summary>
    public const double CreepingDeathPerHaste = 0.5;

    /// <summary>Mara's talent tree, as in game (game data talent-slot records, hero Mara).</summary>
    public static readonly TalentSlot[] TalentTree = TalentSlot.Tree(
        //              row cost  a                            b                           c
        TalentSlot.InRow(1, 2,    RedLedger.TalentId,          CorrosiveSpill.TalentId,    AssassinsGuile.TalentId),
        TalentSlot.InRow(2, 1,    Bloodrush.TalentId,          VenomousDelight.TalentId,   EfficientKiller.TalentId),
        TalentSlot.InRow(3, 2,    GushingBlood.TalentId,       FeedTheQueen.TalentId,      DeadlyScheme.TalentId),
        TalentSlot.InRow(4, 1,    MacabreStratagemTalent.TalentId, MaidensDoom.TalentId,   SeethingBurst.TalentId),
        TalentSlot.InRow(5, 3,    Hemotoxin.TalentId,          SinnersPride.TalentId,      Malevolence.TalentId),
        TalentSlot.InRow(6, 1,    ArachnidOnslaught.TalentId,  CausticWounds.TalentId,     Puncture.TalentId));

    public override IReadOnlyList<TalentSlot> TalentTreeSlots => TalentTree;

    public override MainStat MainStat => HeroMainStat;

    /// <summary>Macabre Stratagem: Final Stratagem is replaced by Macabre Stratagem.</summary>
    public bool HasMacabreStratagem { get; internal set; }

    /// <summary>Multipliers on Energy regeneration (Predator's Rush, Maiden of Death), read as Energy regenerates.</summary>
    public List<Func<SimTime, double>> EnergyRegenMultipliers { get; } = new();

    public double ComboPointsHeld => ResourceAmount(ComboPoints);

    public bool IsStealthed(SimTime now) => Auras.HasAura(BroodingShadows.Stealth, now);

    public override void Initialize()
    {
        AddResource(Energy);
        AddResource(ComboPoints);
        // "Increases the chance to trigger a Spirit Refund whenever you use an ability that costs Combo Points, causing you
        // to refund any Energy and Combo Points spent" (hero 11's Spirit text). The Combo Points come back through the
        // spend; the Energy through OnSpiritRefund below.
        SpiritRefundResource = ComboPoints;
        ClassDamageTuning = HeroBalancingDamageAura;
        GlobalCooldownSeconds = GlobalCooldown;
        GlobalCooldownScalesWithHaste = false;

        Hemorrhage = new Hemorrhage(this);
        SeethingPoison = new SeethingPoison(this);
        VolatilePoison = new VolatilePoison(this);
        CausticPoison = new CausticPoison(this);
        PoisonDamageIds.UnionWith([SeethingPoison.DamageLogId, VolatilePoison.DamageLogId, LogIds.Effect(VolatilePoison.EruptionEffectId),
            CausticPoison.DamageLogId]);
        // The poisons are not abilities of Mara's: their auras are listed here.
        Register(Hemorrhage.Debuff);
        Register(SeethingPoison.Debuff);
        Register(SeethingPoison.PredatorsRush);
        Register(VolatilePoison.Debuff);

        Abilities.Add(new Attack(this));
        Abilities.Add(new Backstab(this));
        Abilities.Add(new StealthBackstab(this));
        Abilities.Add(new WidowsBite(this));
        Abilities.Add(new SkitteringBlades(this));
        Abilities.Add(new StealthSkitteringBlades(this));
        Abilities.Add(new QueensFang(this));
        Abilities.Add(new ArachnidAssault(this));
        Abilities.Add(new HemorrhagingStrike(this));
        Abilities.Add(new BroodingShadows(this));
        Abilities.Add(new Kick(this));
        Abilities.Add(new ShadowWard(this));
        Abilities.Add(new WebCrack(this));
        Abilities.Add(new StalkerStep(this));
        Abilities.Add(new MaidenOfDeath(this));
        Abilities.Add(_matriarch = new MatriarchMacabre(this));
        Abilities.Add(new FinalStratagem(this));
        Abilities.Add(new MacabreStratagem(this));

        // Combat logs (Fc8v6yWxD14GQMnV fight 19 @ 25.691): a Spirit Refund on a Queen's Fang gives back its Combo
        // Points and its Energy.
        OnSpiritRefund += (resource, _, ctx) =>
        {
            if (resource != ComboPoints || _lastFinisherEnergy <= 0) return;
            double energy = _lastFinisherEnergy;
            ctx.Schedule(SimTime.FromSeconds(SpiritRefundDelaySeconds), c => GainEnergy(energy, c), "Spirit Refund");
        };
        // Energy regenerates at the rate held since the last regeneration: haste and the buffs that change the rate are
        // read again whenever an aura comes or goes.
        Auras.OnGained += (_, _, now) => RegenerateEnergy(now);
        Auras.OnRemoved += (_, now) => RegenerateEnergy(now);
        Auras.OnStacksLost += (_, _, now) => RegenerateEnergy(now);
    }

    private MatriarchMacabre? _matriarch;

    /// <summary>Matriarch Macabre: its shadowy clones repeat Queen's Fang and Arachnid Assault.</summary>
    public MatriarchMacabre Matriarch => _matriarch!;

    /// <summary>Hemorrhaging Strike's bleed.</summary>
    public Hemorrhage Hemorrhage { get; private set; } = null!;

    public SeethingPoison SeethingPoison { get; private set; } = null!;
    public VolatilePoison VolatilePoison { get; private set; } = null!;
    public CausticPoison CausticPoison { get; private set; } = null!;

    /// <summary>The damage IDs of Mara's poisons (Venomous Delight); talents that add a poison add its ID.</summary>
    public HashSet<int> PoisonDamageIds { get; } = new();

    // ------------------------------------------------------------------ Energy

    private SimTime _energyRegeneratedAt;
    private double _energyRate;
    /// <summary>Ticks of the regeneration timer per unhasted tick at the last read: Haste and Predator's Rush.</summary>
    private double _energyTickRate;

    /// <summary>Energy per second now: <see cref="EnergyPerSecond"/> raised by haste, times the regeneration buffs.</summary>
    public double EnergyRate(SimTime now) => EnergyRate(now, 1.0 + GetHaste(now));

    private double EnergyRate(SimTime now, double hasteFactor)
    {
        double rate = EnergyPerSecond * hasteFactor;
        foreach (var multiplier in EnergyRegenMultipliers) rate *= multiplier(now);
        return rate;
    }

    private double EnergyTickRate(SimTime now, double hasteFactor) =>
        Auras.HasAura(SeethingPoison.PredatorsRush, now) ? hasteFactor * SeethingPoison.PredatorsRushEnergyRegen : hasteFactor;

    /// <summary>
    /// Adds the Energy regenerated since the last regeneration at the rate then, and reads the rate again. A changed tick rate
    /// restarts the regeneration timer, and a change during the restart adds no time to it.
    /// </summary>
    public void RegenerateEnergy(SimTime now)
    {
        if (now > _energyRegeneratedAt)
        {
            double seconds = (now - _energyRegeneratedAt).TotalSeconds;
            _energyRegeneratedAt = now;
            var state = Resources.Get(Energy);
            state.Current = Math.Min(state.Max, state.Current + _energyRate * seconds);
        }
        double hasteFactor = 1.0 + GetHaste(now);
        _energyRate = EnergyRate(now, hasteFactor);
        double tickRate = EnergyTickRate(now, hasteFactor);
        if (now == _energyRegeneratedAt && Math.Abs(tickRate - _energyTickRate) > 1e-9)
        {
            _energyRegeneratedAt = now + SimTime.FromSeconds(EnergyLostToTimerRestartSeconds);
        }
        _energyTickRate = tickRate;
    }

    public override void RegenerateResources(SimTime now) => RegenerateEnergy(now);

    public double EnergyAt(SimTime now)
    {
        RegenerateEnergy(now);
        return ResourceAmount(Energy);
    }

    /// <summary>The Energy a cast of ability <paramref name="abilityId"/> spends, net of what the cast itself gives; 0 for no ability of Mara's.</summary>
    public double EnergySpentBy(int abilityId) =>
        Abilities.OfType<MaraAbility>().FirstOrDefault(a => a.Id == abilityId)?.NetEnergyCost ?? 0;

    public void SpendEnergy(double amount, SimulationContext ctx)
    {
        RegenerateEnergy(ctx.CurrentTime);
        SpendResource(Energy, amount, ctx);
    }

    public void GainEnergy(double amount, SimulationContext ctx)
    {
        RegenerateEnergy(ctx.CurrentTime);
        GainResource(Energy, amount, ctx);
    }

    /// <summary>
    /// A gain of Energy from an ability: Maiden of Death multiplies it. Hemorrhage's ticks and Efficient
    /// Killer go through it; Widow's Bite, Venomous Delight and a Spirit Refund's Energy do not.
    /// </summary>
    public void GainEnergyWithMaiden(double amount, SimulationContext ctx) =>
        GainEnergy(Auras.HasAura(MaidenOfDeath.Buff, ctx.CurrentTime) ? amount * MaidenOfDeath.EnergyGainMultiplier : amount, ctx);

    protected override void OnFightBegins(SimulationContext ctx)
    {
        _energyRegeneratedAt = ctx.CurrentTime;
        _energyRate = EnergyRate(ctx.CurrentTime);
        _energyTickRate = EnergyTickRate(ctx.CurrentTime, 1.0 + GetHaste(ctx.CurrentTime));
        void Tick(SimulationContext c)
        {
            RegenerateEnergy(c.CurrentTime);
            c.Schedule(SimTime.FromSeconds(EnergyTickSeconds), Tick, "Energy regeneration");
        }
        ctx.Schedule(SimTime.FromSeconds(EnergyTickSeconds), Tick, "Energy regeneration");
    }

    // ------------------------------------------------------------------ Combo Points

    /// <summary>The Energy the last finisher spent: a Spirit Refund on its Combo Points gives it back.</summary>
    private double _lastFinisherEnergy;

    /// <summary>Spends every Combo Point held for a finisher that cost <paramref name="energySpent"/>; returns the points spent.</summary>
    public int SpendComboPoints(SimulationContext ctx, double energySpent)
    {
        int points = (int)ComboPointsHeld;
        _lastFinisherEnergy = energySpent;
        SpendResource(ComboPoints, points, ctx);
        _lastFinisherEnergy = 0;
        return points;
    }

    /// <summary>An attack ended Stealth (Assassin's Guile).</summary>
    public event Action<SimulationContext, MaraAbility>? AttackedFromStealth;

    internal void RaiseAttackedFromStealth(SimulationContext ctx, MaraAbility attack) => AttackedFromStealth?.Invoke(ctx, attack);

    public void GainComboPoints(double amount, SimulationContext ctx, EnemyTarget? from = null) =>
        GainResource(ComboPoints, amount, ctx, from);

    /// <summary>Creeping Death: more poison or bleed damage per Haste.</summary>
    public double CreepingDeath(SimTime now) => 1.0 + CreepingDeathPerHaste * GetHaste(now);

    public override void ResetForIteration()
    {
        ResetCombatState();
        _energyRegeneratedAt = SimTime.Zero;
        _energyRate = 0;
        _lastFinisherEnergy = 0;
        GetAbility<BroodingShadows>()!.ResetState();
    }
}
