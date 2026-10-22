using FellowshipSim.Core.Heroes.Rime.Talents;

namespace FellowshipSim.Core.Heroes.Rime.Abilities;

/// <summary>Ability 1020. 2 charges, cooldown accelerated by haste, generates 1 Winter Orb.</summary>
public class ColdSnap : RimeAbility
{
    public const int AbilityId = 1020;
    public const string AbilityName = "Cold Snap";
    public const double Coefficient = 3.145;
    public const double CooldownSeconds = 12.0;
    public const int Charges = 2;
    public const int WinterOrbsGained = 1;

    public ColdSnap(Rime owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Basic)
    {
        BaseCoefficient = Coefficient;
        // "Cold Snap gains Cooldown Acceleration equal to your Haste": its charges recharge with haste added to the rate.
        SetCooldown(CooldownSeconds, Charges, scalesWithHaste: true);
    }

    private FlightOfTheNavir? _flightOfTheNavir;

    public override void Connect() => _flightOfTheNavir = Rime.GetAbility<FlightOfTheNavir>();

    protected override bool CanCast(SimulationContext ctx, bool explain, out string? reason)
    {
        // Navir's Keeper: "causes your next Cold Snap to not require nor cost any charge"
        if (Rime.Auras.HasAura(NavirsKeeper.Buff, ctx.CurrentTime))
        {
            reason = null;
            return true;
        }
        return base.CanCast(ctx, explain, out reason);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        var hero = Rime;
        if (hero.Auras.HasAura(NavirsKeeper.Buff, ctx.CurrentTime))
        {
            hero.Auras.ConsumeStack(NavirsKeeper.Buff, ctx.CurrentTime);
        }
        else
        {
            TriggerCooldown(ctx);
        }

        hero.GenerateWinterOrbs(WinterOrbsGained, ctx, target);
        DealDamage(ctx, target);
        _flightOfTheNavir?.CommandSwallows(ctx, target);
    }
}
