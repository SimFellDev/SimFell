using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Core.Heroes.Rime.Talents;

/// <summary>Talent 43: Freezing Torrent ticks reduce Bursting Ice's cooldown; Cold Snap reduces Freezing Torrent's.</summary>
public partial class ChillingFinesse : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var burstingIce = hero.GetAbility<BurstingIce>();
        var freezingTorrent = hero.GetAbility<FreezingTorrent>();
        hero.OnChannelTick += (ability, _, _, ctx) =>
        {
            if (ability is FreezingTorrent && burstingIce != null) hero.ReduceCooldown(burstingIce.Name, SimTime.FromSeconds(CastedDebuffAoeDamageCooldownReductionCooldownReductionPerTick), ctx.CurrentTime);
        };
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability is ColdSnap && freezingTorrent != null) hero.ReduceCooldown(freezingTorrent.Name, SimTime.FromSeconds(ReduceCooldownOnChanneledBeamReductionInSeconds), now);
        };
    }
}
