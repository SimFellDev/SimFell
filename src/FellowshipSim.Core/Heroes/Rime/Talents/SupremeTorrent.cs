using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Core.Heroes.Rime.Talents;

/// <summary>Talent 527: Freezing Torrent deals more damage and channels longer.</summary>
public partial class SupremeTorrent : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var torrent = hero.GetAbility<FreezingTorrent>();
        torrent?.AddDamageMultiplier(DamageBonusDamageScale);
        torrent?.ExtendChannel(IncreasedChannelDurationAddedSeconds);
        if (torrent != null) torrent.FullFinalTick = true;
    }
}
