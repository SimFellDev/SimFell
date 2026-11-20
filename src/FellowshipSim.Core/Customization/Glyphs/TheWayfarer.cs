namespace FellowshipSim.Core.Customization.Glyphs;

/// <summary>
/// Glyph 703: "Every interval you gain The Wayfarer, granting you Haste by rank for a duration. Each
/// time you use your [CORE] ability, the time to The Wayfarer's next application is reduced within a range, relative
/// to the cooldown of the [CORE] ability used. Abilities with no cooldown provide a minimum reduction." (buff 3184; the
/// time to the next application is effect 3183, "The Wayfarer will apply again once this expires"). The first
/// application is at the start of the fight.
/// </summary>
public partial class TheWayfarer : Glyph
{
    public const int BuffEffectId = 3184;
    public const int CooldownEffectId = 3183;
    /// <summary>Tooltip: "reduced by 0.2 to 8 seconds"; "Abilities with no cooldown provide 0.2 seconds reduction".</summary>
    public const double MinReductionSeconds = 0.2;
    /// <summary>Tooltip: "reduced by 0.2 to 8 seconds".</summary>
    public const double MaxReductionSeconds = 8.0;
    /// <summary>
    /// A [CORE] ability takes a fraction of its cooldown off the time to the next application (glyph 703 constants
    /// CooldownReduction.MovementAbilityCooldownFractionMultiplier and MovementAbilityCooldownDenominator). Combat
    /// logs: each cycle is <see cref="Constants.Cooldown"/> less that fraction of the cooldown of each Bursting Ice and Freezing Torrent
    /// completed in it (N18KtgP3rvpYGm24 fight 27, Rzg1nQNHpm2fhYtB fight 23), and a cast that takes it past 0 applies
    /// The Wayfarer as it completes.
    /// </summary>
    public const double ReductionPerCooldownSecond = 4.0 / 30.0;

    public static readonly AuraDef Buff = AuraDef.Effect(GlyphName, BuffEffectId, Constants.BuffDuration);

    /// <summary>Effect 3183 (tags System, no RemoveOnDeath): it stays through the hero's death.</summary>
    public static readonly AuraDef OnCooldown = AuraDef.Effect("The Wayfarer: On Cooldown", CooldownEffectId, Constants.Cooldown) with { KeepsOnDeath = true };

    public override int Id => GlyphId;
    public override string Name => GlyphName;

    public override void Bind(Hero hero)
    {
        var buff = Buff.WithModifiers(StatModifier.Add(StatType.Haste, ForRank(Constants.BuffAdditionalHasteByRank)));

        void Grant(SimTime now)
        {
            hero.Auras.Apply(buff, now);
            hero.Auras.Apply(OnCooldown, now);
        }

        // The cooldown running out applies The Wayfarer and starts the cooldown again.
        hero.Auras.OnRemoved += (aura, at) =>
        {
            if (aura.LogId == OnCooldown.LogId && hero.SimContext is { } ctx) Grant(at > ctx.CurrentTime ? at : ctx.CurrentTime);
        };
        // The first application comes with the fight's first ability.
        hero.OnCastStarted += (_, _, now) =>
        {
            if (!hero.Auras.HasAura(OnCooldown, now)) Grant(now);
        };
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability.Category != AbilityCategory.Core) return;
            double reduction = Math.Clamp(ability.Cooldown.TotalSeconds * ReductionPerCooldownSecond, MinReductionSeconds, MaxReductionSeconds);
            if (hero.Auras.Get(OnCooldown, now) is not { } cooldown || cooldown.ExpiresAt == SimTime.Infinite) return;
            if (cooldown.ExpiresAt - SimTime.FromSeconds(reduction) <= now) hero.Auras.Remove(OnCooldown, now);
            else hero.Auras.Extend(OnCooldown, SimTime.FromSeconds(-reduction));
        };
    }
}
