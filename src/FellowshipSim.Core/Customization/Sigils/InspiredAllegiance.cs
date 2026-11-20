namespace FellowshipSim.Core.Customization.Sigils;

/// <summary>
/// Sigil 2: "Your abilities have a chance to reduce the Cooldown of your Weapon Ability and
/// grant you and allies Haste rating for a time." (effect 2112). The same buff
/// from other players is a party buff (Replay/PartyBuffs.cs). UNVERIFIED: the hero's own procs are not yet checked
/// against a log.
/// </summary>
public partial class InspiredAllegiance : Sigil
{
    public const int BuffEffectId = 2112;
    public const double BuffSeconds = 8.0;
    public const string ProcKey = "inspired_allegiance";

    // Combat logs: the buff stays through the hero's death (4 deaths, none removed it).
    public static readonly AuraDef Buff = AuraDef.Effect(SigilName, BuffEffectId, BuffSeconds) with { KeepsOnDeath = true, IsProc = true };

    public override int Id => SigilId;
    public override string Name => SigilName;

    public override void Bind(Hero hero)
    {
        var buff = Buff.WithModifiers(StatModifier.Add(StatType.HasteRating, ForRank(Constants.BuffAdditionalHasteRatingByRank)));
        double cooldownSeconds = ForRank(Constants.WeaponAbilityCooldownReductionInSecondsByRank);
        var rppm = new RealPpm(ProcKey, SigilProcsPerMinute);
        hero.OnCastCompleted += (_, _, now) =>
        {
            if (hero.SimContext is not { } ctx || !rppm.Roll(hero, ctx)) return;
            hero.Auras.Apply(buff, now);
            if (hero.WeaponAbility is { } weapon) hero.ReduceCooldown(weapon.Name, SimTime.FromSeconds(cooldownSeconds), now);
        };
    }
}
