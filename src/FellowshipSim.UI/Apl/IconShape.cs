namespace FellowshipSim.UI.Apl;

/// <summary>The shapes of <see cref="Icon"/>.</summary>
public enum IconShape
{
    /// <summary>The chevron of an expandable row.</summary>
    Chevron,
    Bin,
    Up,
    Down,
    Grip,
    Duplicate,
    /// <summary>A star outline; its parent fills the path to mark it on.</summary>
    Star,
    Pencil,
    Save,
    Plus,
    /// <summary>An arrow down onto a line: a build or a list brought in.</summary>
    Import,
    /// <summary>An arrow up off a line: a build or a list written out.</summary>
    Export,
    /// <summary>An arrow out of a box: a link to send.</summary>
    Link,
    /// <summary>An arrow turning back: edits undone.</summary>
    Revert,
    /// <summary>An arrow down into a tray: a file saved to the computer.</summary>
    Download,
    /// <summary>Two arrows in a circle: a build read again from a log.</summary>
    Refresh,
    /// <summary>A play arrow against a bar: one fight stepped through in the APL Debugger.</summary>
    Debug,
}
