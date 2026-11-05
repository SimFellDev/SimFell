namespace FellowshipSim.Core.Domain;

/// <summary>
/// The application of an effect an action list reads (<c>debuff.&lt;name&gt;.oldest.remains</c>): every application as
/// one, the application applied first, the application applied last, or the application that expires first
/// (<c>min:debuff.&lt;name&gt;.remains</c>). An effect with one application reads the same for each.
/// </summary>
public enum TargetEffectPick
{
    All,
    Oldest,
    Newest,
    ExpiresFirst,
}
