namespace FellowshipSim.Core.Domain;

/// <summary>
/// The application of an effect an action list reads (<c>debuff.&lt;name&gt;.oldest.remains</c>): every application as
/// one, the application applied first, or the application applied last. An effect with one application reads the same for
/// each.
/// </summary>
public enum TargetEffectPick
{
    All,
    Oldest,
    Newest,
}
