using FellowshipSim.Core.APL.Debugging;

namespace FellowshipSim.Web;

/// <summary>A traced fight's answer from a worker: its trace, or the reason the engine refused it.</summary>
public sealed record TraceReply(AplFightTrace? Trace, string? Error);
