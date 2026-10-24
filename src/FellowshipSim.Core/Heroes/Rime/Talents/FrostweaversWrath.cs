using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Core.Heroes.Rime.Talents;

/// <summary>
/// Talent 191 / effect 2305 (20s, 2 stacks): gaining Winter Orbs has a 20% chance to grant a stack; the next Glacial Blast
/// or Ice Comet (Talon Strike / Rising Talons) that deals damage has +100% crit chance (overflow is a grievous crit).
/// hero_data "ChanceOnOrbGain" (ProcChance 0.2, CritChance 1.0). Rolled once per gain that adds orbs, as
/// the old C++ sim did. Combat logs (D1fBhVPkwYKXWbpr fight 1, ZPTw4tmBJnK7hCb1 fight 4) have 155 stacks for 848 Winter
/// Orbs in 685 rises. UNVERIFIED: whether the 20% rolls per orb or per gain.
/// </summary>
public class FrostweaversWrath : Talent
{
    public const int TalentId = 191;
    public const string TalentName = "Frostweaver's Wrath";
    public const int EffectId = 2305;
    /// <summary>Datamine export 26136736: effect 2305 duration 20 and the tooltip's "20 seconds" (12 before the patch).</summary>
    public const double BuffSeconds = 20.0;
    public const int MaxStacks = 2;
    public const double ProcChance = 0.2;
    public const double CritChance = 1.0;
    public const string ProcKey = "frostweavers_wrath";

    public static readonly AuraDef Buff = AuraDef.Effect("Frostweaver's Wrath", EffectId, BuffSeconds, MaxStacks);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        // "Each time you generate Winter Orbs" (one roll per orb gained), and when a Spirit Refund gives orbs back: combat
        // logs (D1fBhVPkwYKXWbpr fight 1) have 16 of 107 gains with no orb gained, each 0.199-0.204s after a spend whose
        // Spirit Refund applied Rising Spirit, as the refunded orbs arrive.
        void Roll(ResourceDef resource, SimulationContext ctx)
        {
            if (resource == Rime.WinterOrbsResource && ctx.Outcomes.RollProc(ProcKey, ProcChance, ctx)) hero.Auras.Apply(Buff, ctx.CurrentTime);
        }
        hero.OnResourceGained += (resource, _, _, ctx) => Roll(resource, ctx);
        hero.OnResourceRefunded += (resource, _, ctx) => Roll(resource, ctx);
        // "Your next Glacial Blast or Ice Comet that deals damage" critically strikes: Talon Strike and Rising Talons count
        // (they are built on them). An Ice Comet's impact uses it up as it strikes, whenever the comet was cast, free
        // comets too: combat logs (D1fBhVPkwYKXWbpr fight 1) remove the buff within 2ms of every grievous Ice Comet impact,
        // and a gain after the cast still makes the impact grievous (181.610: gained 33ms before the impact, cast 902ms
        // before; 248.592: 31ms and 599ms; 441.337: 166ms and 868ms).
        var comet = hero.GetAbility<IceComet>();
        comet?.ImpactCritChanceBonuses.Add(ctx =>
        {
            if (!hero.Auras.HasAura(Buff, ctx.CurrentTime)) return 0.0;
            hero.Auras.ConsumeStack(Buff, ctx.CurrentTime);
            return CritChance;
        });
        // A Glacial Blast uses it up as its projectile strikes, too: combat logs (D1fBhVPkwYKXWbpr fight 1,
        // ZPTw4tmBJnK7hCb1 fight 4) remove the buff with every Glacial Blast hit it was up for (34 and 12), each of them
        // grievous, and 14 and 2 of them had the buff gained after the cast completed (D1fBhVPkwYKXWbpr 343.768: cast
        // 343.134, gained 343.335; ZPTw4tmBJnK7hCb1 520.400: cast 519.698, gained 519.833).
        var blast = hero.Abilities.FirstOrDefault(a => a.GetType() == typeof(GlacialBlast));
        if (blast != null)
        {
            blast.HitCritChanceBonuses.Add(ctx => hero.Auras.HasAura(Buff, ctx.CurrentTime) ? CritChance : 0.0);
            hero.OnDamageDealtBy(blast.DamageLogId, (in dmg) =>
            {
                if (hero.Auras.HasAura(Buff, dmg.Timestamp)) hero.Auras.ConsumeStack(Buff, dmg.Timestamp);
            });
        }
        hero.OnCastResolving += cast =>
        {
            if (cast.Ability == comet || cast.Ability == blast || cast.Ability is not (GlacialBlast or IceComet) || !hero.Auras.HasAura(Buff, cast.Time)) return;
            hero.Auras.ConsumeStack(Buff, cast.Time);
            cast.CritChance += CritChance;
        };
    }
}
