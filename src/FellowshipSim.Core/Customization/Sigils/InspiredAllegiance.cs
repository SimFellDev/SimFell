namespace FellowshipSim.Core.Customization.Sigils;

/// <summary>
/// Sigil 2: "Your abilities have a chance to reduce the Cooldown of your Weapon Ability by 1.5 / 4 / 6 / 8 seconds and
/// grant you and 1 / 2 / 3 / 3 allies 6 / 10 / 13 / 17 Haste rating for 8 seconds. (1.2 PPM)" (effect 2112). The same buff
/// from other players is a party buff (Replay/PartyBuffs.cs). UNVERIFIED: the hero's own procs are not yet checked
/// against a log.
/// </summary>
public class InspiredAllegiance : Sigil
{
    public const int SigilId = 2;
    public const string SigilName = "Inspired Allegiance";
    public const int BuffEffectId = 2112;
    public const double BuffSeconds = 8.0;
    public const double ProcsPerMinute = 1.2;
    public const string ProcKey = "inspired_allegiance";
    public static readonly double[] HasteRatingByRank = [6, 10, 13, 17];
    public static readonly double[] WeaponCooldownSecondsByRank = [1.5, 4, 6, 8];

    // Combat logs: the buff stays through the hero's death (4 deaths, none removed it).
    public static readonly AuraDef Buff = AuraDef.Effect(SigilName, BuffEffectId, BuffSeconds) with { KeepsOnDeath = true, IsProc = true };

    public override int Id => SigilId;
    public override string Name => SigilName;

    public override void Bind(Hero hero)
    {
        var buff = Buff.WithModifiers(StatModifier.Add(StatType.HasteRating, ForRank(HasteRatingByRank)));
        double cooldownSeconds = ForRank(WeaponCooldownSecondsByRank);
        var rppm = new RealPpm(ProcKey, ProcsPerMinute);
        hero.OnCastCompleted += (_, _, now) =>
        {
            if (hero.SimContext is not { } ctx || !rppm.Roll(hero, ctx)) return;
            hero.Auras.Apply(buff, now);
            if (hero.WeaponAbility is { } weapon) hero.ReduceCooldown(weapon.Name, SimTime.FromSeconds(cooldownSeconds), now);
        };
    }
}
