using FellowshipSim.Core.Heroes.Gunde;
using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 698: Heart Splitter has extra Charges, a chance to strike the target one additional time and a Critical
/// Strike chance bonus against targets at high health. Combat logs (Y4wpb2f3gc8LqJDX fight 25): the extra strike lands
/// shortly after the cast, with its own Exsanguinate; it adds no cast event.
/// </summary>
public partial class BleedingHearts : Legendary
{
    /// <summary>
    /// Combat logs (Gunde's NOTES.md): the extra strike comes 0.23-0.30s after the cast, sooner than the game data's
    /// <see cref="ChargesDoubleStrikeDelayBetweenStrikes"/>.
    /// </summary>
    public const double ExtraStrikeDelaySeconds = 0.23;
    public const double HighHealthThreshold = 0.8;
    public const double HighHealthCritChance = 1.0;
    public const string ProcKey = "bleeding_hearts";

    public override void Bind(Hero hero)
    {
        if (hero is not Gunde gunde || gunde.GetAbility<HeartSplitter>() is not { } heartSplitter) return;
        heartSplitter.SetCharges(ChargesNumOfCharges);
        heartSplitter.TargetCritChanceBonuses.Add(target => target.HealthFraction > HighHealthThreshold ? HighHealthCritChance : 0.0);
        heartSplitter.Cast += (ctx, target, rend) =>
        {
            if (!ctx.Outcomes.RollProc(ProcKey, ChargesDoubleStrikeChance, ctx, target)) return;
            ctx.Schedule(SimTime.FromSeconds(ExtraStrikeDelaySeconds), c =>
            {
                if (target is { IsSleeping: false }) heartSplitter.Strike(c, target, rend, extra: true);
            }, Name);
        };
    }
}
