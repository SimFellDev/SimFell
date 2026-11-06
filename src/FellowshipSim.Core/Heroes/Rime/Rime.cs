using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Customization.WeaponAbilities;
using FellowshipSim.Core.Heroes.Rime.Abilities;
using FellowshipSim.Core.Heroes.Rime.Talents;

namespace FellowshipSim.Core.Heroes.Rime;

/// <summary>
/// Rime, the Harbinger of Winter. Her abilities build Anima; Anima become Winter Orbs (<see cref="AnimaPerWinterOrb"/> each), which her spenders use.
/// Abilities live in Abilities/, talents in Talents/ (each talent hooks the events below or the abilities' own events).
/// </summary>
public sealed class Rime : Hero
{
    public const int MaxWinterOrbs = 5;
    public const double AnimaPerWinterOrb = 9.0;
    /// <summary>
    /// Undocumented in the game (game data ability 1033 lists no source for it):
    /// every Winter Orb earned sends Frost Swallows at enemies.
    /// </summary>
    public const int SwallowsPerWinterOrb = 3;

    public static readonly ResourceDef Anima = new("Anima", Max: 100);

    public static readonly ResourceDef WinterOrbsResource = new("Winter Orbs", Max: MaxWinterOrbs)
    {
        Aliases = ["worb"],
    };

    /// <summary>Class tuning: hero attribute BalancingDamageAura (game data).</summary>
    public const double BalancingDamageAura = 1.04;

    /// <summary>Spirit Points per Spirit Refund (game data).</summary>
    public const double SpiritPointsPerRefund = 2.0;

    /// <summary>Rime's talent tree, as in game (players write picks as row + column: "1b + 2a + 2c ...").</summary>
    public static readonly TalentSlot[] TalentTree = TalentSlot.Tree(
        //              row cost  a                            b                              c
        TalentSlot.InRow(1, 2,    Avalanche.TalentId,          CoalescingFrost.TalentId,      GlacialAssault.TalentId),
        TalentSlot.InRow(2, 1,    ColdShower.TalentId,         ChillingFinesse.TalentId,      Burstbolter.TalentId),
        TalentSlot.InRow(3, 2,    CascadingBlitz.TalentId,     HarrowingIce.TalentId,         IcyFlow.TalentId),
        TalentSlot.InRow(4, 1,    BurstingSwallows.TalentId,   GreaterGlacialBlast.TalentId,  NavirsKeeper.TalentId),
        TalentSlot.InRow(5, 3,    IcyTalons.TalentId,          FrostweaversWrath.TalentId,    SoulfrostTorrent.TalentId),
        TalentSlot.InRow(6, 1,    BitingCold.TalentId,         SupremeTorrent.TalentId,       WisdomOfTheNorth.TalentId));

    /// <summary>Icy Talons: Glacial Blast and Ice Comet are replaced by Talon Strike and Rising Talons.</summary>
    public bool HasIcyTalons { get; internal set; }

    public override IReadOnlyList<TalentSlot> TalentTreeSlots => TalentTree;

    /// <summary>Game data hero 17: mainStat Intellect.</summary>
    public override MainStat MainStat => MainStat.Intellect;

    public int WinterOrbs
    {
        get => (int)ResourceAmount(WinterOrbsResource);
        set => Resources.Get(WinterOrbsResource).Current = value;
    }

    public int GlacialAssaultStacks => Auras.GetStacks(GlacialAssault.Buff);

    public override void Initialize()
    {
        AddResource(Anima);
        AddResource(WinterOrbsResource);
        SpiritRefundResource = WinterOrbsResource;
        ClassDamageTuning = BalancingDamageAura;
        SpiritPointsPerSpiritRefund = SpiritPointsPerRefund;

        Abilities.Add(new ColdSnap(this));
        Abilities.Add(new GlacialBlast(this));
        Abilities.Add(new TalonStrike(this));
        Abilities.Add(new RisingTalons(this));
        Abilities.Add(_burstingIce = new BurstingIce(this));
        Abilities.Add(new FreezingTorrent(this));
        Abilities.Add(new FrostBolt(this));
        Abilities.Add(new IceComet(this));
        Abilities.Add(new IceBlitz(this));
        Abilities.Add(new WintersBlessing(this));
        Abilities.Add(_flightOfTheNavir = new FlightOfTheNavir(this));
        Abilities.Add(new WrathOfWinter(this));
        Abilities.Add(_frostSwallows = new FrostSwallows(this));
        Abilities.Add(new IceDash(this));
        Abilities.Add(new BrainFreeze(this));
        Abilities.Add(new FrostWard(this));
        Abilities.Add(new FrigidWinds(this));
    }

    private BurstingIce? _burstingIce;
    private FlightOfTheNavir? _flightOfTheNavir;
    private FrostSwallows? _frostSwallows;

    /// <summary>Spends Winter Orbs (a Spirit Refund may give them back).</summary>
    public void SpendWinterOrbs(int count, SimulationContext? ctx = null) => SpendResource(WinterOrbsResource, count, ctx);

    /// <summary>Gains Winter Orbs one at a time; each earned orb also sends Frost Swallows at enemies.</summary>
    public void GenerateWinterOrbs(int count, SimulationContext? ctx = null, EnemyTarget? target = null)
    {
        for (int i = 0; i < count; i++)
        {
            if (ctx != null) _frostSwallows?.AttackEnemies(ctx, SwallowsPerWinterOrb);
            GainResource(WinterOrbsResource, 1, ctx, target);
        }
    }

    /// <summary>Adds Anima; each <see cref="AnimaPerWinterOrb"/> Anima become a Winter Orb.</summary>
    public void AddAnima(double amount, SimulationContext? ctx = null, EnemyTarget? target = null)
    {
        var primaryTarget = target ?? CurrentTarget ?? ctx?.ActiveEnemies.FirstOrDefault();
        GainResource(Anima, amount, ctx, primaryTarget);

        while (ResourceAmount(Anima) >= AnimaPerWinterOrb)
        {
            Resources.Consume(Anima, AnimaPerWinterOrb);
            GenerateWinterOrbs(1, ctx, primaryTarget);
        }
    }

    public override void ResetForIteration()
    {
        ResetCombatState();
        _burstingIce?.ResetState();
        _flightOfTheNavir?.ResetState();
    }
}
