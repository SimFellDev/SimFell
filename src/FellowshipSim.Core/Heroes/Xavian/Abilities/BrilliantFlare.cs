namespace FellowshipSim.Core.Heroes.Xavian.Abilities;

/// <summary>
/// Ability 1860: "Heal target ally for (752% of Strength) health. When you have a charge of Swift Reprieval or Omega
/// Reprieval your Brilliant Flare turns into Brilliant Flash." Cast time 1.5s (hasted), 132 Mana. No damage.
/// </summary>
public class BrilliantFlare : XavianAbility
{
    public const int AbilityId = 1860;
    public const string AbilityName = "Brilliant Flare";
    public const double CastTimeSeconds = 1.5;
    /// <summary>Game data cost 132 Mana.</summary>
    public const double Cost = 132;

    /// <summary>Raised as the cast resolves (Sun's Verdict).</summary>
    public event Action<SimulationContext>? Flared;

    public BrilliantFlare(Xavian owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Core)
    {
        CastTime = SimTime.FromSeconds(CastTimeSeconds);
        ManaCost = Cost;
    }

    protected override bool CanCast(SimulationContext ctx, bool explain, out string? reason)
    {
        var now = ctx.CurrentTime;
        if (Xavian.SwiftReprievals(now) > 0 || Xavian.Auras.HasAura(OmegaReprieval.Buff, now))
        {
            reason = explain ? $"{Name} is Brilliant Flash while Swift Reprieval or Omega Reprieval is held" : null;
            return false;
        }
        return base.CanCast(ctx, explain, out reason);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        PayMana(ctx);
        Flared?.Invoke(ctx);
    }
}
