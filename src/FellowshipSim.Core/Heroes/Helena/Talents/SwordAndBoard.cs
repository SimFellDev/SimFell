using FellowshipSim.Core.Heroes.Helena.Abilities;

namespace FellowshipSim.Core.Heroes.Helena.Talents;

/// <summary>
/// Talent 209: "Your Power Strike has a 50% chance to instantly replenish 1 charge of Shield Slam and grant you Empowered
/// Shield Slam for 12 seconds, causing your next Shield Slam to apply an absorb on you." UNVERIFIED: one log has the talent
/// (D1fBhVPkwYKXWbpr fight 1).
/// </summary>
public class SwordAndBoard : Talent
{
    public const int TalentId = 209;
    public const string TalentName = "Sword & Board";
    public const double Chance = 0.5;
    public const string ProcKey = "sword_and_board";

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<PowerStrike>() is not { } powerStrike || hero.GetAbility<ShieldSlam>() is not { } shieldSlam) return;
        powerStrike.Struck += ctx =>
        {
            if (!ctx.Outcomes.RollProc(ProcKey, Chance, ctx)) return;
            hero.CooldownOf(shieldSlam).GrantCharge(ctx.CurrentTime);
            hero.Auras.Apply(HoldTheLine.Empowered, ctx.CurrentTime);
        };
    }
}
