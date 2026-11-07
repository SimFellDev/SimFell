using FellowshipSim.Core.Heroes.Xavian.Abilities;

namespace FellowshipSim.Core.Heroes.Xavian.Talents;

/// <summary>Talent 573: "Your Omnistrike has a 50% chance to reset the cooldown of your Solar Blades ability."</summary>
public class RisingSun : Talent
{
    public const int TalentId = 573;
    public const string TalentName = "Rising Sun";
    public const double ResetChance = 0.5;
    public const string ProcKey = "rising_sun";

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var xavian = (Xavian)hero;
        xavian.GetAbility<Omnistrike>()!.Struck += ctx =>
        {
            if (ctx.Outcomes.RollProc(ProcKey, ResetChance, ctx)) xavian.CooldownOf(xavian.SolarBlades).ResetAll();
        };
    }
}
