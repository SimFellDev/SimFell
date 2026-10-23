using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>
/// Talent 676 / effect 3177 (12 stacks): "Each time you cast Detonate you gain 1 stack of Cascading Inferno, up to 12
/// stacks. When you have at least 4 stacks your next Infernal Wave is Instant Cast, has +100% Critical Strike chance, and
/// generates twice the amout of Cinders." That Infernal Wave uses 4 stacks. UNVERIFIED: no combat log has this talent.
/// </summary>
public class CascadingInferno : Talent
{
    public const int TalentId = 676;
    public const string TalentName = "Cascading Inferno";
    public const int EffectId = 3177;
    public const int MaxStacks = 12;
    public const int StacksUsed = 4;
    public const double CritChance = 1.0;
    public const double CinderMultiplier = 2.0;

    public static readonly AuraDef Buff = AuraDef.Effect("Cascading Inferno", EffectId, maxStacks: MaxStacks);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var wave = hero.GetAbility<InfernalWave>();
        if (wave == null) return;
        bool Ready(SimTime now) => hero.Auras.GetStacks(Buff, now) >= StacksUsed;
        wave.InstantCastSources.Add(Ready);
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability is Detonate) hero.Auras.Apply(Buff, now);
        };
        // The empowered wave is decided as it resolves: the cast consumes the stacks.
        bool empowered = false;
        hero.OnCastResolving += cast =>
        {
            empowered = false;
            if (cast.Ability != wave || !Ready(cast.Time)) return;
            empowered = true;
            hero.Auras.ConsumeStack(Buff, cast.Time, StacksUsed);
            cast.CritChance += CritChance;
        };
        wave.CinderMultipliers.Add(cast => empowered && cast.Ability == wave ? CinderMultiplier : 1.0);
    }
}
