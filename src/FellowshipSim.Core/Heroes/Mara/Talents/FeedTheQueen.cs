using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Core.Heroes.Mara.Talents;

/// <summary>
/// Talent 121 / effect 2195 (12s, 6 stacks): "Each time your Skittering Blades deals damage you are granted Feed the
/// Queen, causing the next Queen's Fang you perform to deal 12% more damage per stack." Combat logs (Fc8v6yWxD14GQMnV
/// fight 19): a stack per enemy hit; the next Queen's Fang uses up every stack.
/// </summary>
public class FeedTheQueen : Talent
{
    public const int TalentId = 121;
    public const string TalentName = "Feed the Queen";
    public const int EffectId = 2195;
    public const double BuffSeconds = 12.0;
    public const int MaxStacks = 6;
    public const double DamagePerStack = 0.12;

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, EffectId, BuffSeconds, MaxStacks);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        foreach (var blades in hero.Abilities.OfType<SkitteringBlades>())
        {
            blades.Hit += (in hit) => hero.Auras.Apply(Buff, hit.Timestamp);
        }
        // Combat logs (3 fights, 441 clone hits): the shadowy clones' Queen's Fangs deal 0.71x what the cast's bonuses give
        // them, the Feed the Queen bonus of 3 to 4 stacks; the clones have every other bonus.
        hero.GetAbility<QueensFang>()?.OwnHitMultipliers.Add(ctx =>
        {
            int stacks = hero.Auras.GetStacks(Buff, ctx.CurrentTime);
            if (stacks == 0) return 1.0;
            hero.Auras.Remove(Buff, ctx.CurrentTime);
            return 1.0 + DamagePerStack * stacks;
        });
    }
}
