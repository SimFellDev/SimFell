using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Core.Heroes.Rime.Talents;

/// <summary>Talent 38. Cold Snap grants Glacial Assault stacks; at <see cref="MaxStacks"/> the next Glacial Blast/Talon Strike is empowered.</summary>
public partial class GlacialAssault : Talent
{
    public const int BuffEffectId = 1377;

    /// <summary>Stacks from Cold Snap; at <see cref="MaxStacks"/> the next Glacial Blast or Talon Strike is empowered.</summary>
    public static readonly AuraDef Buff = AuraDef.Effect("Glacial Assault", BuffEffectId, maxStacks: MaxStacks);

    public override string Name => TalentName;
    public override int Id => TalentId;

    /// <summary>Tooltip: Cold Snap grants a stack of Glacial Assault. (Glacial Blast and Talon Strike use the stacks.)</summary>
    public override void Bind(Hero hero)
    {
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability is ColdSnap) hero.Auras.Apply(Buff, now);
        };
    }
}
