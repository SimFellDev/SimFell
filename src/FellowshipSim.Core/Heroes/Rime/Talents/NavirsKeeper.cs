using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Core.Heroes.Rime.Talents;

/// <summary>Talent 189: Flight of the Navir grants stacks of Navir's Keeper (free Cold Snaps).</summary>
public partial class NavirsKeeper : Talent
{
    public const int BuffEffectId = 2730;

    /// <summary>"causes your next Cold Snap to not require nor cost any charge"</summary>
    public static readonly AuraDef Buff = AuraDef.Effect("Navir's Keeper", BuffEffectId, maxStacks: Constants.GrantInstantSingleDamageChargeNumCharges);

    public override string Name => TalentName;
    public override int Id => TalentId;

    /// <summary>Flight of the Navir grants the stacks; Cold Snap reads and consumes them.</summary>
    public override void Bind(Hero hero)
    {
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability is FlightOfTheNavir) hero.Auras.Apply(Buff, now, setStacks: Constants.GrantInstantSingleDamageChargeNumCharges);
        };
    }
}
