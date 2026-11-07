using FellowshipSim.Core.Heroes.Gunde;
using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 698 (Gunde's Band of The Bleeding Heart, item 5220; Northlander's Harness, item 5898): Heart Splitter
/// has extra Charges, a chance to strike the target one additional time and a Critical Strike chance bonus against
/// targets at high health. Combat logs (Y4wpb2f3gc8LqJDX fight 25): the extra strike lands shortly after the cast,
/// with its own Exsanguinate; it adds no cast event.
/// </summary>
public class BleedingHearts : Legendary
{
    public const int PowerId = 698;
    public const int RingItemId = 5220;
    public const int HarnessItemId = 5898;
    public const int Charges = 2;
    public const double ExtraStrikeChance = 0.25;
    public const double ExtraStrikeDelaySeconds = 0.23;
    public const double HighHealthThreshold = 0.8;
    public const double HighHealthCritChance = 1.0;
    public const string ProcKey = "bleeding_hearts";

    public override int LegendaryPowerId => PowerId;
    public override string Name => "Bleeding Hearts";
    public override string HeroName => "Gunde";
    public override IReadOnlyList<int> ItemIds => [RingItemId, HarnessItemId];

    public override void Bind(Hero hero)
    {
        if (hero is not Gunde gunde || gunde.GetAbility<HeartSplitter>() is not { } heartSplitter) return;
        heartSplitter.SetCharges(Charges);
        heartSplitter.TargetCritChanceBonuses.Add(target => target.HealthFraction > HighHealthThreshold ? HighHealthCritChance : 0.0);
        heartSplitter.Cast += (ctx, target, rend) =>
        {
            if (!ctx.Outcomes.RollProc(ProcKey, ExtraStrikeChance, ctx, target)) return;
            ctx.Schedule(SimTime.FromSeconds(ExtraStrikeDelaySeconds), c =>
            {
                if (target is { IsSleeping: false }) heartSplitter.Strike(c, target, rend, extra: true);
            }, Name);
        };
    }
}
