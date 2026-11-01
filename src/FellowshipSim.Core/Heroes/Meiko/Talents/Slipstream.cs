using FellowshipSim.Core.Heroes.Meiko.Abilities;

namespace FellowshipSim.Core.Heroes.Meiko.Talents;

/// <summary>
/// Talent 99: "Each time you cast Gust you release a burst of winds at your starting location, causing all enemies within
/// 750 radius to have 50% reduced movement speed for 6 seconds" (effect 1513, MoveSpeed x0.5). Positions are not
/// simulated: every enemy in combat is near.
/// </summary>
public class Slipstream : Talent
{
    public const int TalentId = 99;
    public const string TalentName = "Slipstream";
    public const int DebuffEffectId = 1513;
    public const double DebuffSeconds = 6.0;

    public static readonly AuraDef Debuff = AuraDef.Effect(TalentName, DebuffEffectId, DebuffSeconds);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<Gust>() is not { } gust) return;
        gust.Dashed += ctx =>
        {
            foreach (var enemy in ctx.ActiveEnemies) enemy.Auras.Apply(Debuff, ctx.CurrentTime);
        };
    }
}
