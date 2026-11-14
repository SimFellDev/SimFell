using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Core.Heroes.Rime.Talents;

/// <summary>
/// Talent 48: Ice Comet has a chance to cause one additional Ice Comet to strike the target, and a smaller chance to
/// cause more (game data). Rolled as a chance of at least one extra comet (<see cref="AnyExtraChance"/>),
/// then a chance of a second (<see cref="SecondExtraChance"/>): the same odds, one roll per extra comet. Rolls when a comet
/// strikes (a cast comet, Cold Shower's, or Rising Talons' first wave); the extra comets cannot trigger it again (per the user).
/// Combat logs (n7zxK8harZT2wjDB fight 9): each extra comet strikes a fixed number of server frames after
/// the impact before it.
/// </summary>
public partial class Avalanche : Talent
{
    /// <summary>The chance of at least one extra comet, and of a second one once there is one.</summary>
    public const double AnyExtraChance = Constants.MultiStrikeChanceToStrikeTwice + Constants.MultiStrikeChanceToStrikeTrice;
    public const double SecondExtraChance = Constants.MultiStrikeChanceToStrikeTrice / AnyExtraChance;
    public const double CometSpacingSeconds = 5.0 / 30.0;
    public const string ProcKey = "avalanche";
    public const string SecondProcKey = "avalanche_second";

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var comet = hero.GetAbility<IceComet>();
        if (comet == null) return;
        // Every comet that strikes: Rising Talons is built on Ice Comet, so its first wave counts too.
        foreach (var source in hero.Abilities.OfType<IceComet>()) source.Struck += (ctx, target) =>
        {
            if (!ctx.Outcomes.RollProc(ProcKey, AnyExtraChance, ctx)) return;
            int extra = ctx.Outcomes.RollProc(SecondProcKey, SecondExtraChance, ctx) ? 2 : 1;
            for (int i = 1; i <= extra; i++)
            {
                comet.FreeCast(ctx, target, source: this, countsAsCast: false, impactDelaySeconds: CometSpacingSeconds * i);
            }
        };
    }
}
