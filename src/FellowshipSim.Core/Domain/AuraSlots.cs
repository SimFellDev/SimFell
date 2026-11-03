using System.Collections.Concurrent;

namespace FellowshipSim.Core.Domain;

/// <summary>
/// Each aura key's slot: a small number of its own, the same for every <see cref="AuraDef"/> with the key. An
/// <see cref="AuraContainer"/> finds an aura by its slot without hashing the key.
/// </summary>
internal static class AuraSlots
{
    private static readonly ConcurrentDictionary<string, int> Slots = new(StringComparer.Ordinal);
    private static int _count;

    /// <summary>The slots handed out so far.</summary>
    public static int Count => Volatile.Read(ref _count);

    public static int Of(string key) => Slots.GetOrAdd(key, static _ => Interlocked.Increment(ref _count) - 1);
}
