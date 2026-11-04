using FellowshipSim.Engine.Jobs;
using FellowshipSim.UI.Services;
using FellowshipSim.UI.Shared;

namespace FellowshipSim.UI.Replay;

/// <summary>
/// The Import page's report and the review it started, kept for the circuit or window while another page shows.
/// </summary>
public sealed class ImportPageState
{
    public string Link { get; set; } = "";

    public Report? Report { get; set; }

    /// <summary>The fight and player picked to import (fight ID, player ID); at first the ones the link names.</summary>
    public (int FightId, int PlayerId)? Picked { get; set; }

    /// <summary>True to list every fight of a report whose link names one; false lists that fight alone.</summary>
    public bool ShowAllFights { get; set; }

    public MessageState Status { get; set; } = MessageState.None;

    /// <summary>The review started from the page; its player buttons are disabled while it runs.</summary>
    public SimJob? ReviewJob { get; set; }

    public string? StartError { get; set; }
}
