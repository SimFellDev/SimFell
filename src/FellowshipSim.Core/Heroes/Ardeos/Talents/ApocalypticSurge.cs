using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>
/// Talent 678 / effect 3176 (18s, 4 stacks): "Your Apocalypse has 1.5 seconds reduced cast time and grants you 2 stacks of
/// Apocalyptic Surge for 18 seconds, up to 4 stacks maximum. Your next Detonate consumes 1 stack of Apocalyptic Surge and
/// is Free."
/// </summary>
public class ApocalypticSurge : Talent
{
    public const int TalentId = 678;
    public const string TalentName = "Apocalyptic Surge";
    public const int EffectId = 3176;
    public const double BuffSeconds = 18.0;
    public const int MaxStacks = 4;
    public const int StacksGranted = 2;
    public const double CastTimeReductionSeconds = 1.5;

    public static readonly AuraDef Buff = AuraDef.Effect("Apocalyptic Surge", EffectId, BuffSeconds, MaxStacks);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var apocalypse = hero.GetAbility<Apocalypse>();
        if (apocalypse == null) return;
        apocalypse.CastTimeReductionSeconds += CastTimeReductionSeconds;
        hero.GetAbility<Detonate>()?.FreeCastBuffs.Add(Buff);
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability is Apocalypse) hero.Auras.Apply(Buff, now, stacks: StacksGranted);
        };
    }
}
