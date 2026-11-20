using FellowshipSim.Core.Heroes.Rime.Talents;

namespace FellowshipSim.Core.Heroes.Rime.Abilities;

/// <summary>Ability 1020. Charges, cooldown accelerated by haste, generates a Winter Orb.</summary>
public partial class ColdSnap : RimeAbility
{
    public ColdSnap(Rime owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = Constants.DamageIntellectCoefficient;
        // "Cold Snap gains Cooldown Acceleration equal to your Haste": its charges recharge with haste added to the rate.
        SetCooldown(AbilityCooldown, AbilityCharges, scalesWithHaste: true);
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

        hero.GenerateWinterOrbs(Constants.OrbGain, ctx, target);
        DealDamage(ctx, target);
        _flightOfTheNavir?.CommandSwallows(ctx, target);
    }
}
