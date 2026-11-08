using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>
/// Talent 678 / effect 3176: Apocalypse has a reduced cast time and grants stacks of Apocalyptic Surge. The next Detonate
/// consumes a stack and is free.
/// </summary>
public partial class ApocalypticSurge : Talent
{
    public const int EffectId = 3176;

    public static readonly AuraDef Buff = AuraDef.Effect("Apocalyptic Surge", EffectId, BuffDuration, MaxStacks);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var apocalypse = hero.GetAbility<Apocalypse>();
        if (apocalypse == null) return;
        apocalypse.CastTimeReductionSeconds += ReducedCastTimeCastTimeReductionInSeconds;
        hero.GetAbility<Detonate>()?.FreeCastBuffs.Add(Buff);
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability is Apocalypse) hero.Auras.Apply(Buff, now, stacks: StacksGranted);
        };
    }
}
