namespace FellowshipSim.UI.Pages;

/// <summary>The Log Replay page's sub-tabs.</summary>
public enum LogReplayTab { Import, Reviews }

/// <summary>The Log Replay page's sub-tab, kept for the circuit or window while another page shows.</summary>
public sealed class LogReplayState
{
    public LogReplayTab Tab { get; set; } = LogReplayTab.Import;
}
