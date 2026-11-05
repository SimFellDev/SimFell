using FellowshipSim.Core.Heroes.Mara.Abilities;
using FellowshipSim.Core.Heroes.Mara.Talents;

namespace FellowshipSim.Core.Heroes.Mara;

/// <summary>
/// Mara, the Hungering Blade. Her generators build Combo Points, and her finishers spend them. Every ability but Widow's Bite
/// costs Energy, and Energy regenerates. Backstab, Widow's Bite and Skittering Blades used from Stealth (Brooding Shadows)
/// apply a poison. Abilities live in Abilities/, talents in Talents/.
/// </summary>
public sealed class Mara : Hero
{
    /// <summary>Hero attributes MaxResources 200 and Resources 200 (game data hero 11): Mara starts every fight with full Energy.</summary>
    public const double MaxEnergy = 200;

    /// <summary>Hero attribute MaxResourcesSecond 6.</summary>
    public const double MaxComboPoints = 6;

    /// <summary>
    /// Energy regenerated per second before haste and buffs: AC_Hero_Mara Mara.Energy.GainAmount 0.15 every Mara.Energy.GainInterval
    /// 0.01s (`GA_Mara_Passive_EnergyManager` offsets 72 and 1277 to 1782), the interval divided by Haste and by 1.2 with
    /// Predator's Rush, and `MaraUtils_GainEnergy` offsets 232 to 507 multiply the amount by 1.2 with Maiden of Death.
    /// Combat logs: log over this rate has a median of 0.997 to 0.999 in the four buff states, over 11,258 pairs of readings in the
    /// 23 Mara fights of data/fights.
    /// </summary>
    public const double EnergyPerSecond = 15.0;

    /// <summary>
    /// Seconds between two Energy regeneration ticks of the engine. A cast and a replay's check read the Energy regenerated up
    /// to their moment (<see cref="RegenerateEnergy"/>); the ticks let an action list waiting for Energy act again. Speed:
    /// 20,000 sims of the fight 19 profile take 5.7s with 0.1s ticks and 4.5s with 1s ticks, for the same DPS (12,060 and
    /// 12,062).
    /// </summary>
    public const double EnergyTickSeconds = 0.1;

    /// <summary>
    /// Regeneration a restart of the tick timer loses. `GA_Mara_Passive_EnergyManager`: a change of Haste (watcher at offset 856) or
    /// of Predator's Rush (offset 2284) ends the TickAsync timer (offset 691) and starts a new one after DelayUntilNextTick
    /// (offset 732), TickAtStart false, so one frame and the old timer's remainder are lost, and a second change in that frame finds no
    /// watcher. Maiden of Death scales the amount in `MaraUtils_GainEnergy` with no restart. The remainder, up to 0.01s / Haste, and
    /// the order of the timer and latent ticks in a frame have no decoded value, so the size is a log pattern: over the 8 players with
    /// no Venomous Delight in the 23 Mara fights of data/fights (T2qLPpmMkrAtJ9B1 fight 18, 3M2vWr8taxVfTQFY fight 53 and NOTES.md),
    /// log minus engine Energy with no frame lost moves 0.035 per second over 4,666s with no change, -0.64 at a Haste drop (478 steps),
    /// -0.85 at a Haste rise (383) and -0.63 and -0.79 at a Predator's Rush change (59 and 38), 0.02s to 0.04s of regeneration at rates
    /// of 15 to 30 per second.
    /// </summary>
    public const double EnergyLostToTimerRestartSeconds = PeriodicEffect.ServerFrameSeconds;

    public static readonly ResourceDef Energy = new("Energy", Max: MaxEnergy, Initial: MaxEnergy);

    public static readonly ResourceDef ComboPoints = new("Combo Points", Max: MaxComboPoints)
    {
        Aliases = ["cp"],
    };

    /// <summary>
    /// Combat logs (Fc8v6yWxD14GQMnV fight 19, 999 casts in a row): casts on the global cooldown come 1.032s apart at the
    /// least, with Spirit of Heroism (+30% Haste) too. Mara's global cooldown is 1s, not hasted.
    /// </summary>
    public const double GlobalCooldown = 1.0;

    /// <summary>Class tuning: hero attribute BalancingDamageAura 1.04 (game data hero 11), +4%.</summary>
    public const double BalancingDamageAura = 1.04;

    /// <summary>
    /// "CREEPING DEATH: Caustic Poison, Seething Poisons, and Volatile Poison deal 0.5% increased Poison damage for every
    /// 1% Haste you have"; Hemorrhaging Strike's bleed, Corrosive Spill and Seething Burst too (their tooltips).
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

    /// <summary>Datamine hero 11: mainStat Agility.</summary>
    public override MainStat MainStat => MainStat.Agility;

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
        ClassDamageTuning = BalancingDamageAura;
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

        // Combat logs (Fc8v6yWxD14GQMnV fight 19 @ 25.691): a Spirit Refund on a Queen's Fang gives back its 6 Combo
        // Points and its 40 Energy.
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
    /// A gain through `MaraUtils_GainEnergy` (offsets 232 to 507): Maiden of Death multiplies it. Hemorrhage's ticks and Efficient
    /// Killer go through it; Widow's Bite, Venomous Delight and a Spirit Refund's Energy do not.
    /// </summary>
    public void GainEnergyWithMaiden(double amount, SimulationContext ctx) =>
        GainEnergy(Auras.HasAura(MaidenOfDeath.Buff, ctx.CurrentTime) ? amount * MaidenOfDeath.EnergyRegen : amount, ctx);

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

    /// <summary>Creeping Death: 0.5% more poison or bleed damage per 1% Haste.</summary>
    public double CreepingDeath(SimTime now) => 1.0 + CreepingDeathPerHaste * GetHaste(now);

    public override void ResetForIteration()
    {
        ResetCombatState();
        _energyRegeneratedAt = SimTime.Zero;
        _energyRate = 0;
        _lastFinisherEnergy = 0;
    }
}
