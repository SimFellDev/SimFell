using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>
/// Talent 145: each stack of Searing Blaze on a target increases the damage it deals, up to a maximum. Every tick on that
/// target adds a stack. All stacks are lost when Searing Blaze is removed from the target. A tick deals damage with the stacks held before it. UNVERIFIED: no combat log has
/// this talent.
/// </summary>
public class AgonizingBlaze : Talent
{
    public const int TalentId = 145;
    public const string TalentName = "Agonizing Blaze";
    public const double DamagePerStack = 0.04;
    public const int MaxStacks = 10;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var searing = hero.GetAbility<SearingBlaze>()?.Dot;
        if (searing == null) return;
        searing.DamagePerTickDealt = DamagePerStack;
        searing.MaxTicksCounted = MaxStacks;
    }
}
