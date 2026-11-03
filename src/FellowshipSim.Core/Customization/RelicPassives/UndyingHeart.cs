namespace FellowshipSim.Core.Customization.RelicPassives;

/// <summary>
/// Relic passive 779 (Orb of Gathering Storms): "Your abilities have a chance (0.8 PPM) to increase your Haste by 25% for
/// 20 seconds." (ability 2327, effect 3270 Dark Prophecy) Rolled on every ability used. The game data's 5 second cooldown
/// does not hold between procs: 12 procs come less than 5 seconds after the one before on the same hero, the shortest
/// 1.97 seconds (1p7AXmHfwV9CYMxR fight 30, dBgbn8wD1RxL7Kma fight 9, Fc8v6yWxD14GQMnV fight 19, fXyzwGcQkTrLD34R fight 4,
/// pWmcV42K39GHTLQX fight 23).
/// </summary>
public class UndyingHeart : RelicPassive
{
    public const int RelicPassiveId = 779;
    public const string RelicPassiveName = "Undying Heart";
    public const int OrbItemId = 5924;
    public const string OrbItemName = "Orb of Gathering Storms";
    public const int EffectId = 3270;
    public const double ProcsPerMinute = 0.8;
    public const double DurationSeconds = 20.0;
    public const double HasteBonus = 0.25;
    public const string ProcKey = "undying_heart";

    public static readonly AuraDef Buff = AuraDef.Effect("Dark Prophecy", EffectId, DurationSeconds,
        modifiers: StatModifier.Add(StatType.Haste, HasteBonus)) with { IsProc = true };

    public override int Id => RelicPassiveId;
    public override string Name => RelicPassiveName;
    public override IReadOnlyList<RelicItem> Items => [new(OrbItemId, OrbItemName)];

    public override void Bind(Hero hero)
    {
        var rppm = new RealPpm(ProcKey, ProcsPerMinute);
        hero.OnCastCompleted += (_, _, now) =>
        {
            if (hero.SimContext is { } ctx && rppm.Roll(hero, ctx)) hero.Auras.Apply(Buff, now);
        };
    }
}
