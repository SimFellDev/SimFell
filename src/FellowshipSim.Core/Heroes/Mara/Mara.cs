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
    /// Energy regenerated per second before haste and buffs. The game data has no value. Combat logs (Fc8v6yWxD14GQMnV
    /// fight 19, 13% Haste): a median of 17.3 per second with no buff, 15.3 before Haste. The median holds a few Venomous
    /// Delight gains, so the engine takes 15. UNVERIFIED to better than 0.5 per second.
    /// </summary>
    public const double EnergyPerSecond = 15.0;

    /// <summary>
    /// Seconds between two Energy regeneration ticks of the engine. A cast reads the Energy regenerated up to its moment
    /// (<see cref="RegenerateEnergy"/>); the ticks let an action list waiting for Energy act again, and keep the Energy a
    /// replay reads within 3 of the moment. Speed: 20,000 sims of the fight 19 profile take 5.7s with 0.1s ticks and
    /// 4.5s with 1s ticks, for the same DPS (12,060 and 12,062).
    /// </summary>
    public const double EnergyTickSeconds = 0.1;

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

    /// <summary>Energy per second now: <see cref="EnergyPerSecond"/> raised by haste, times the regeneration buffs.</summary>
    public double EnergyRate(SimTime now)
    {
        double rate = EnergyPerSecond * (1.0 + GetHaste(now));
        foreach (var multiplier in EnergyRegenMultipliers) rate *= multiplier(now);
        return rate;
    }

    /// <summary>Adds the Energy regenerated since the last regeneration at the rate then, and reads the rate again.</summary>
    public void RegenerateEnergy(SimTime now)
    {
        if (now <= _energyRegeneratedAt)
        {
            if (now == _energyRegeneratedAt) _energyRate = EnergyRate(now);
            return;
        }
        double seconds = (now - _energyRegeneratedAt).TotalSeconds;
        _energyRegeneratedAt = now;
        var state = Resources.Get(Energy);
        state.Current = Math.Min(state.Max, state.Current + _energyRate * seconds);
        _energyRate = EnergyRate(now);
    }

    public double EnergyAt(SimTime now)
    {
        RegenerateEnergy(now);
        return ResourceAmount(Energy);
    }

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

    protected override void OnFightBegins(SimulationContext ctx)
    {
        _energyRegeneratedAt = ctx.CurrentTime;
        _energyRate = EnergyRate(ctx.CurrentTime);
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
