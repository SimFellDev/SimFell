using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>
/// Talent 676 / effect 3177: each Detonate cast gains a stack. With enough stacks the next Infernal Wave is instant, gains
/// critical strike chance and generates more Cinders. Combat logs (1p7AXmHfwV9CYMxR fight 30, dBgbn8wD1RxL7Kma fight 9):
/// each Detonate adds a stack and each instant Infernal Wave takes the stacks it needs. UNVERIFIED: the wave's crit chance
/// and Cinders.
/// </summary>
public partial class CascadingInferno : Talent
{
    public const int EffectId = 3177;

    public static readonly AuraDef Buff = AuraDef.Effect("Cascading Inferno", EffectId, maxStacks: Constants.MaxStacks);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var wave = hero.GetAbility<InfernalWave>();
        if (wave == null) return;
        bool Ready(SimTime now) => hero.Auras.GetStacks(Buff, now) >= Constants.StacksThreshold;
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
            hero.Auras.ConsumeStack(Buff, cast.Time, Constants.StacksThreshold);
            cast.CritChance += Constants.AdditionalCritChance;
        };
        wave.CinderMultipliers.Add(cast => empowered && cast.Ability == wave ? Constants.ResourceMultiplier : 1.0);
    }
}
