namespace FellowshipSim.Core.Customization.RelicPassives;

/// <summary>
/// Relic passive 778 (Orb of Awakened Gaze): "Your critical strikes have a chance to increase your Primary Stat for a
/// time." (ability 2326, effect 3268 Draconic Might) The game data's cooldown does not hold
/// between procs: 16 procs come before it has passed after the one before on the same hero
/// (Fc8v6yWxD14GQMnV fight 19, fXyzwGcQkTrLD34R fight 4, YC861NTRgLbmw4Qa fight 31). The roll has Haste off and the rate
/// row ArmorSetBonus.PowerBonusOnCrit.PPM × (1 + CritChance), from the game data; the tooltip's rate is its own text.
/// </summary>
public class DragonboneDice : RelicPassive
{
    public const int RelicPassiveId = 778;
    public const string RelicPassiveName = "Dragonbone Dice";
    public const int OrbItemId = 5925;
    public const string OrbItemName = "Orb of Awakened Gaze";
    public const int BuffEffectId = 3268;
    public const double BuffSeconds = 14.0;
    public const double MainStatMultiplier = 1.18;
    public const double ProcsPerMinute = 1.0;
    public const string ProcKey = "dragonbone_dice";

    public static readonly AuraDef Buff = AuraDef.Effect("Draconic Might", BuffEffectId, BuffSeconds,
        modifiers: StatModifier.More(StatType.PrimaryAttributeMultiplier, MainStatMultiplier)) with { IsProc = true };

    public override int Id => RelicPassiveId;
    public override string Name => RelicPassiveName;
    public override IReadOnlyList<RelicItem> Items => [new(OrbItemId, OrbItemName)];

    public override void Bind(Hero hero)
    {
        var rppm = new RealPpm(ProcKey, ProcsPerMinute, usesHaste: false, rateScale: now => 1.0 + hero.CritChance(now));
        hero.OnDamageDealt += (in dmg) =>
        {
            if (dmg.IsCrit && dmg.SimContext is { } ctx && rppm.Roll(hero, ctx)) hero.Auras.Apply(Buff, ctx.CurrentTime);
        };
    }
}
