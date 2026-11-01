using FellowshipSim.Core.Heroes.Xavian.Abilities;

namespace FellowshipSim.Core.Heroes.Xavian.Talents;

/// <summary>
/// Talent 607: "Your Brilliant Flash applies Sun's Touch to the target, causing an additional 30% of the damage or 30% of
/// the healing to be dealt over 12 seconds. Additional Brilliant Flash damage or healing is accumulated into any already
/// existing Sun's Touch on the target." Effect 2725 (damage, 12s, period 1.5). UNVERIFIED: no combat log has this talent.
/// </summary>
public class SunsTouch : Talent
{
    public const int TalentId = 607;
    public const string TalentName = "Sun's Touch";
    public const int DotEffectId = 2725;
    public const double DamageShare = 0.3;
    public const double DurationSeconds = 12.0;
    public const double PeriodSeconds = 1.5;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var xavian = (Xavian)hero;
        var dot = new XavianDot(xavian, DotEffectId, TalentName, PeriodSeconds, DurationSeconds);
        hero.Register(dot.Debuff);
        xavian.BrilliantFlash.Dealt += (ctx, enemy, dealt) => dot.Apply(ctx, enemy, dealt * DamageShare);
    }
}
