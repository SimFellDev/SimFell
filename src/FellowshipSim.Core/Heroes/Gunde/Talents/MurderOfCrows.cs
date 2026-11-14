using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Core.Heroes.Gunde.Talents;

/// <summary>
/// Talent 758 / effect 3250: "Your Rupture grants you stacks of Murder of Crows for a time. Your next
/// ability that applies Rend consumes a stack of Murder of Crows and always triggers a Spirit Refund." UNVERIFIED: no
/// combat log has this talent.
/// </summary>
public partial class MurderOfCrows : Talent
{
    public const int EffectId = 3250;
    public const double BuffSeconds = 12.0;
    public const int Stacks = 2;

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, EffectId, BuffSeconds, Stacks);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability is Rupture) hero.Auras.Apply(Buff, now, setStacks: Stacks);
        };
        // Gunde rolls his Spirit Refund only for abilities that apply Rend: a stack makes that roll certain.
        hero.GuaranteedSpiritRefunds.Add(now =>
        {
            if (!hero.Auras.HasAura(Buff, now)) return false;
            hero.Auras.ConsumeStack(Buff, now);
            return true;
        });
    }
}
