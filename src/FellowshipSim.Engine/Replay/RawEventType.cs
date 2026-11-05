namespace FellowshipSim.Engine.Replay;

/// <summary>A raw fight event's <c>type</c>. The aura types are last, from <see cref="ApplyBuff"/>.</summary>
internal enum RawEventType : byte
{
    Other,
    DungeonStart,
    CombatantInfo,
    Death,
    Resurrect,
    Damage,
    BeginCast,
    Cast,
    BeginChannel,
    EndChannel,
    ApplyBuff,
    ApplyBuffStack,
    RemoveBuff,
    RemoveBuffStack,
    RefreshBuff,
    ApplyDebuff,
    ApplyDebuffStack,
    RemoveDebuff,
    RemoveDebuffStack,
    RefreshDebuff,
}
