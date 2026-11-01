using FellowshipSim.Core.Heroes.Meiko.Abilities;

namespace FellowshipSim.Core.Heroes.Meiko.Talents;

/// <summary>
/// Talent 96: "Each hit with Earthfist Barrage adds 1 stack to your Serenity ability. Serenity now prevents your Stone
/// Shield from losing health for 0.1 seconds per stack of Serenity consumed." The stacks count for Everlasting Spirit's
/// pulse; the stones' health changes no damage.
/// </summary>
public class Earthwell : Talent
{
    public const int TalentId = 96;
    public const string TalentName = "Earthwell";

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<EarthfistBarrage>() is not { } barrage) return;
        barrage.Punched += (ctx, _) => hero.Auras.Apply(Serenity.Stacks, ctx.CurrentTime);
    }
}
