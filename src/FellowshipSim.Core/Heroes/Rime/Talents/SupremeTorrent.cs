using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Core.Heroes.Rime.Talents;

/// <summary>Talent 527: Freezing Torrent deals more damage and channels longer.</summary>
public class SupremeTorrent : Talent
{
    public const int TalentId = 527;
    public const string TalentName = "Supreme Torrent";
    public const double DamageMultiplier = 1.2;
    public const double AddedChannelSeconds = 0.8;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var torrent = hero.GetAbility<FreezingTorrent>();
        torrent?.AddDamageMultiplier(DamageMultiplier);
        torrent?.ExtendChannel(AddedChannelSeconds);
        if (torrent != null) torrent.FullFinalTick = true;
    }
}
