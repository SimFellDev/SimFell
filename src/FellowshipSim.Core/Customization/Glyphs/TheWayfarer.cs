namespace FellowshipSim.Core.Customization.Glyphs;

/// <summary>
/// Glyph 703: "Every 60 seconds you gain The Wayfarer, granting you +6% / +10% / +16% / +25% Haste for 14 seconds. Each
/// time you use your [CORE] ability, the time to The Wayfarer's next application is reduced by 0.2 to 8 seconds, relative
/// to the cooldown of the [CORE] ability used. Abilities with no cooldown provide 0.2 seconds reduction." (buff 3184; the
/// time to the next application is effect 3183, "The Wayfarer will apply again once this expires"). The first
/// application is at the start of the fight.
/// </summary>
public class TheWayfarer : Glyph
{
    public const int GlyphId = 703;
    public const string GlyphName = "The Wayfarer";
    public const int BuffEffectId = 3184;
    public const int CooldownEffectId = 3183;
    public const double CooldownSeconds = 60.0;
    public const double BuffSeconds = 14.0;
    public const double MinReductionSeconds = 0.2;
    public const double MaxReductionSeconds = 8.0;
    /// <summary>
    /// A [CORE] ability takes its cooldown x 4 / 30 off the time to the next application (glyph 703 constants
    /// CooldownReduction.MovementAbilityCooldownFractionMultiplier 4 and MovementAbilityCooldownDenominator 30). Combat
    /// logs: each cycle is 60s less 1.333s per Bursting Ice and 2.0s per Freezing Torrent completed in it
    /// (N18KtgP3rvpYGm24 fight 27, Rzg1nQNHpm2fhYtB fight 23: 13 cycles), and a cast that takes it past 0 applies
    /// The Wayfarer as it completes.
    /// </summary>
    public const double ReductionPerCooldownSecond = 4.0 / 30.0;
    public static readonly double[] HasteByRank = [0.06, 0.10, 0.16, 0.25];

    public static readonly AuraDef Buff = AuraDef.Effect(GlyphName, BuffEffectId, BuffSeconds);

    /// <summary>Effect 3183 (tags System, no RemoveOnDeath): it stays through the hero's death.</summary>
    public static readonly AuraDef Cooldown = AuraDef.Effect("The Wayfarer: On Cooldown", CooldownEffectId, CooldownSeconds) with { KeepsOnDeath = true };

    public override int Id => GlyphId;
    public override string Name => GlyphName;

    public override void Bind(Hero hero)
    {
        var buff = Buff.WithModifiers(StatModifier.Add(StatType.Haste, ForRank(HasteByRank)));

        void Grant(SimTime now)
        {
            hero.Auras.Apply(buff, now);
            hero.Auras.Apply(Cooldown, now);
        }

        // The cooldown running out applies The Wayfarer and starts the cooldown again.
        hero.Auras.OnRemoved += (aura, at) =>
        {
            if (aura.LogId == Cooldown.LogId && hero.SimContext is { } ctx) Grant(at > ctx.CurrentTime ? at : ctx.CurrentTime);
        };
        // The first application comes with the fight's first ability.
        hero.OnCastStarted += (_, _, now) =>
        {
            if (!hero.Auras.HasAura(Cooldown, now)) Grant(now);
        };
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability.Category != AbilityCategory.Core) return;
            double reduction = Math.Clamp(ability.Cooldown.TotalSeconds * ReductionPerCooldownSecond, MinReductionSeconds, MaxReductionSeconds);
            if (hero.Auras.Get(Cooldown, now) is not { } cooldown || cooldown.ExpiresAt == SimTime.Infinite) return;
            if (cooldown.ExpiresAt - SimTime.FromSeconds(reduction) <= now) hero.Auras.Remove(Cooldown, now);
            else hero.Auras.Extend(Cooldown, SimTime.FromSeconds(-reduction));
        };
    }
}
