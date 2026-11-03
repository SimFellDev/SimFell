using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Core.Heroes.Rime.Talents;

/// <summary>
/// Talent 191 / effect 2305 (20s, 2 stacks): gaining Winter Orbs has a 20% chance to grant a stack; the next Glacial Blast
/// or Ice Comet (Talon Strike / Rising Talons) that deals damage has +100% crit chance (overflow is a grievous crit).
/// hero_data "ChanceOnOrbGain" (ProcChance 0.2, CritChance 1.0). Rolled once per gain that adds orbs, as
/// the old C++ sim did. Combat logs (D1fBhVPkwYKXWbpr fight 1, ZPTw4tmBJnK7hCb1 fight 4) have 155 stacks for 848 Winter
/// Orbs in 685 rises. UNVERIFIED: whether the 20% rolls per orb or per gain. A Talon Strike or Rising Talons uses one stack
/// on its first hit alone (its first spike, its first wave), not on every spike or wave (per the user; no log has it yet).
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

    public static readonly AuraDef Buff = AuraDef.Effect("Frostweaver's Wrath", EffectId, BuffSeconds, MaxStacks) with { IsProc = true };

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
        // Rising Talons is an Ice Comet: its first wave is its impact.
        double UseOnImpact(SimulationContext ctx)
        {
            if (!hero.Auras.HasAura(Buff, ctx.CurrentTime)) return 0.0;
            hero.Auras.ConsumeStack(Buff, ctx.CurrentTime);
            return CritChance;
        }
        foreach (var comets in hero.Abilities.OfType<IceComet>()) comets.ImpactCritChanceBonuses.Add(UseOnImpact);
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
        // A Talon Strike's spikes: the first to land uses a stack and crits, the cast's other spikes do not.
        var talons = hero.GetAbility<TalonStrike>();
        bool talonFirstSpike = false;
        if (talons != null)
        {
            talons.HitCritChanceBonuses.Add(ctx => talonFirstSpike && hero.Auras.HasAura(Buff, ctx.CurrentTime) ? CritChance : 0.0);
            hero.OnDamageDealtBy(talons.DamageLogId, (in dmg) =>
            {
                if (!talonFirstSpike) return;
                talonFirstSpike = false;
                if (hero.Auras.HasAura(Buff, dmg.Timestamp)) hero.Auras.ConsumeStack(Buff, dmg.Timestamp);
            });
        }
        hero.OnCastResolving += cast =>
        {
            if (cast.Ability == talons)
            {
                talonFirstSpike = true;
                return;
            }
            // Comets and Glacial Blasts use it up as they strike, above.
            if (cast.Ability is IceComet || cast.Ability == blast || cast.Ability is not GlacialBlast || !hero.Auras.HasAura(Buff, cast.Time)) return;
            hero.Auras.ConsumeStack(Buff, cast.Time);
            cast.CritChance += CritChance;
        };
    }
}
