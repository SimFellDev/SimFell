namespace FellowshipSim.UI.Shell;

/// <summary>What a <see cref="DesktopTab"/> shows.</summary>
public enum DesktopTabKind
{
    /// <summary>A page of the menu. These tabs are always there.</summary>
    Page,
    Job,
    Review,

    /// <summary>The APL Debugger, opened from a sim result or the APL Editor.</summary>
    Debugger,
}
