using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Core.Heroes.Rime.Talents;

/// <summary>Talent 43: Freezing Torrent ticks reduce Bursting Ice's cooldown by 0.2s; Cold Snap reduces Freezing Torrent's by 1.5s.</summary>
public class ChillingFinesse : Talent
{
    public const int TalentId = 43;
    public const string TalentName = "Chilling Finesse";
    public const double BurstingIceReductionSeconds = 0.2;
    public const double FreezingTorrentReductionSeconds = 1.5;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var burstingIce = hero.GetAbility<BurstingIce>();
        var freezingTorrent = hero.GetAbility<FreezingTorrent>();
        hero.OnChannelTick += (ability, _, _, ctx) =>
        {
            if (ability is FreezingTorrent && burstingIce != null) hero.ReduceCooldown(burstingIce.Name, SimTime.FromSeconds(BurstingIceReductionSeconds), ctx.CurrentTime);
        };
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability is ColdSnap && freezingTorrent != null) hero.ReduceCooldown(freezingTorrent.Name, SimTime.FromSeconds(FreezingTorrentReductionSeconds), now);
        };
    }
}
