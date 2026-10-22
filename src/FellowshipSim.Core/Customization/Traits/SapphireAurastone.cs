namespace FellowshipSim.Core.Customization.Traits;

/// <summary>
/// Trait 32: "Your Spirit Ability causes you to conjure a Sapphire Aurastone ... The Sapphire Aurastone lasts as long as
/// the Spirit of Heroism effect from your Spirit Ability is active on you. While active, 3.4% / 6.3% / 9.1% / 12% of all
/// damage you deal is accumulated into the Sapphire Aurastone and released in a pulse of damage, evenly split between
/// nearby enemies every 3 seconds." (damage effect 2347; it never crits and is not scaled again)
/// </summary>
public class SapphireAurastone : Trait
{
    public const int TraitId = 32;
    public const string TraitName = "Sapphire Aurastone";
    public const int DamageEffectId = 2347;
    public const string DamageName = "Sapphire Aurastone Damage";
    public const double PulseSeconds = 3.0;
    /// <summary>
    /// The first pulse comes 6s after the stone appears, then every 3s (n7zxK8harZT2wjDB fight 9: Spirit of Heroism at
    /// 16.604s and 92.447s, first pulses at 22.615s and 98.455s, holding 6s of damage).
    /// </summary>
    public const double FirstPulseSeconds = 6.0;
    public static readonly double[] StoredShareByRank = [0.034, 0.063, 0.091, 0.12];

    public override int Id => TraitId;
    public override string Name => TraitName;

    public override void Bind(Hero hero)
    {
        double share = ForRank(StoredShareByRank);
        int logId = LogIds.Effect(DamageEffectId);
        double stored = 0.0;
        int stone = 0;
        bool active = false;

        void Pulse(SimulationContext ctx, int id)
        {
            if (!active || id != stone) return;
            var struck = stored > 0.0 ? ctx.Outcomes.SelectTargets(logId, ctx.ActiveEnemies, ctx) : [];
            // With no enemy near it, the stone keeps what it holds for the next pulse.
            if (struck.Count > 0)
            {
                double each = stored / struck.Count;
                stored = 0.0;
                foreach (var enemy in struck)
                {
                    ctx.ApplyDamage(hero, enemy, each, logId, DamageName, canCrit: false, 0.0, 1.0);
                }
            }
            ctx.Schedule(SimTime.FromSeconds(PulseSeconds), c => Pulse(c, id), DamageName);
        }

        hero.SpiritOfHeroismStarted += ctx =>
        {
            active = true;
            stored = 0.0;
            int id = ++stone;
            ctx.Schedule(SimTime.FromSeconds(FirstPulseSeconds), c => Pulse(c, id), DamageName);
        };
        hero.SpiritOfHeroismEnded += _ => active = false;
        hero.OnDamageDealt += dmg =>
        {
            if (active && dmg.AbilityId != logId) stored += share * dmg.Amount;
        };
    }
}
