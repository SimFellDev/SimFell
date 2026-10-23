using FellowshipSim.Core.Heroes.Mara;
using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 420 (Mara's Wristbands of Vexira's Prey, item 5225; Hungering Blade Leggings, item 5900): "Your
/// Queen's Fang and Arachnid Assault critical strikes apply Vexira's Venom to enemies, adding 28% of the initial damage
/// accumulatively as poison damage dealt over 6 seconds" (effect 2364, a tick every 2 seconds). UNVERIFIED: no combat
/// log has it; a new crit adds its share to what is left and the 6 seconds start again, ticks unhasted and unable to
/// crit.
/// </summary>
public class VexirasVenom : Legendary
{
    public const int PowerId = 420;
    public const int WristItemId = 5225;
    public const int LegsItemId = 5900;
    public const int EffectId = 2364;
    public const double DamageShare = 0.28;
    public const double VenomSeconds = 6.0;
    public const double TickSeconds = 2.0;

    public static readonly AuraDef Debuff = AuraDef.Effect("Vexira's Venom", EffectId, VenomSeconds);

    public override int LegendaryPowerId => PowerId;
    public override string Name => "Vexira's Venom";
    public override string HeroName => "Mara";
    public override IReadOnlyList<int> ItemIds => [WristItemId, LegsItemId];

    /// <summary>The damage left on each enemy, and a version that ends the ticks of an earlier application.</summary>
    private readonly Dictionary<EnemyTarget, (double Left, int Ticks, int Version)> _venom = new();
    private SimulationContext? _fight;

    public override void Bind(Hero hero)
    {
        if (hero is not Mara mara) return;
        int damageId = LogIds.Effect(EffectId);
        mara.PoisonDamageIds.Add(damageId);
        int ticks = (int)(VenomSeconds / TickSeconds);
        void OnCrit(in DamageContext hit)
        {
            if (!hit.IsCrit || hit.Amount <= 0 || hit.Target is not { } target || hit.SimContext is not { } ctx) return;
            if (_fight != ctx)
            {
                _venom.Clear();
                _fight = ctx;
            }
            var (left, _, version) = _venom.GetValueOrDefault(target);
            int current = version + 1;
            _venom[target] = (left + DamageShare * hit.Amount, ticks, current);
            target.Auras.Apply(Debuff, ctx.CurrentTime);
            for (int k = 1; k <= ticks; k++)
            {
                ctx.Schedule(SimTime.FromSeconds(TickSeconds * k), c =>
                {
                    if (!_venom.TryGetValue(target, out var state) || state.Version != current || target.IsSleeping) return;
                    double damage = state.Left / state.Ticks;
                    _venom[target] = (state.Left - damage, state.Ticks - 1, current);
                    c.ApplyDamage(mara, target, damage, damageId, Name, canCrit: false, 0.0, 1.0);
                    if (state.Ticks == 1) _venom.Remove(target);
                }, Name);
            }
        }
        foreach (var finisher in new Ability?[] { mara.GetAbility<QueensFang>(), mara.GetAbility<ArachnidAssault>() })
        {
            if (finisher != null) finisher.Hit += OnCrit;
        }
    }
}
