using System.Text.Json;
using System.Text.Json.Serialization;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Engine.Replay;

/// <summary>
/// A hero-agnostic replay script produced by tools/replay/build_replay_script.py.
/// <see cref="Commands"/> drive the engine; <see cref="Observations"/> are only used to verify it.
/// </summary>
public class ReplayScript
{
    public const string SchemaVersion = "fellowship-replay/2";

    [JsonPropertyName("schema")] public string Schema { get; set; } = string.Empty;
    [JsonPropertyName("report_code")] public string ReportCode { get; set; } = string.Empty;
    [JsonPropertyName("fight_id")] public int FightId { get; set; }
    [JsonPropertyName("fight_name")] public string FightName { get; set; } = string.Empty;
    /// <summary>When the fight was logged (UTC): balance changes in force then apply.</summary>
    [JsonPropertyName("fight_date")] public DateTime? FightDate { get; set; }
    [JsonPropertyName("duration_s")] public double DurationSeconds { get; set; }
    [JsonPropertyName("player")] public ReplayPlayer Player { get; set; } = new();
    [JsonPropertyName("character")] public CharacterBuild? Character { get; set; }
    /// <summary>Every player's augmentation ranks, by player ID (buffs they give scale with their rank).</summary>
    [JsonPropertyName("players")] public Dictionary<string, ReplayPartyMember> Players { get; set; } = new();
    [JsonPropertyName("commands")] public List<ReplayCommand> Commands { get; set; } = new();
    [JsonPropertyName("observations")] public ReplayObservations Observations { get; set; } = new();

    public static ReplayScript FromJson(string json)
    {
        var script = JsonSerializer.Deserialize<ReplayScript>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                     ?? throw new InvalidOperationException("Failed to read replay script");
        if (script.Schema != SchemaVersion)
        {
            throw new InvalidOperationException($"Unsupported replay schema '{script.Schema}', expected '{SchemaVersion}'. Rebuild it with tools/replay/build_replay_script.py.");
        }
        return script;
    }

    public static ReplayScript Load(string path) => FromJson(File.ReadAllText(path));
}

public class ReplayPlayer
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("hero")] public string Hero { get; set; } = string.Empty;
}

public class ReplayPartyMember
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("gear_traits")] public Dictionary<string, int> GearTraits { get; set; } = new();
    /// <summary>Weapon-tree points by log augmentation ID (3,000,000 + datamine augmentation ID).</summary>
    [JsonPropertyName("weapon_traits")] public Dictionary<string, int> WeaponTraits { get; set; } = new();
}

public record UnitRef(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("instance")] int Instance)
{
    public override string ToString() => $"{Id}.{Instance}";
}

public static class CommandKind
{
    public const string EnemyArise = "enemy_arise";
    public const string EnemyDemise = "enemy_demise";
    public const string EnemyHealth = "enemy_health";
    public const string ExternalAura = "external_aura";
    public const string Cast = "cast";
    public const string BeginCast = "begin_cast";
    public const string CompleteCast = "complete_cast";
    public const string CancelCast = "cancel_cast";
    public const string StopChannel = "stop_channel";
    public const string PlayerDeath = "player_death";
    public const string PlayerRevive = "player_revive";
}

public class ReplayCommand
{
    [JsonPropertyName("t")] public double Time { get; set; }
    [JsonPropertyName("kind")] public string Kind { get; set; } = string.Empty;

    // casts
    [JsonPropertyName("ability_id")] public int AbilityId { get; set; }
    [JsonPropertyName("ability_name")] public string AbilityName { get; set; } = string.Empty;
    [JsonPropertyName("target")] public UnitRef? Target { get; set; }
    [JsonPropertyName("inferred_time")] public bool InferredTime { get; set; }

    // enemies
    [JsonPropertyName("enemy")] public UnitRef? Enemy { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("max_hp")] public double MaxHp { get; set; }
    [JsonPropertyName("hp")] public double Hp { get; set; }
    [JsonPropertyName("reason")] public string? Reason { get; set; }

    // external auras
    [JsonPropertyName("type")] public string? AuraEventType { get; set; }
    [JsonPropertyName("source")] public UnitRef? Source { get; set; }
    [JsonPropertyName("stack")] public int? Stack { get; set; }
    [JsonPropertyName("duration_ms")] public double? DurationMs { get; set; }

    public bool IsPlayerAction => Kind is CommandKind.Cast or CommandKind.BeginCast;

    public override string ToString() => Kind switch
    {
        CommandKind.EnemyArise or CommandKind.EnemyDemise => $"{Time:F3} {Kind} {Name} {Enemy}",
        CommandKind.ExternalAura => $"{Time:F3} {AuraEventType} {AbilityName} ({Source} -> {Target})",
        _ => $"{Time:F3} {Kind} {AbilityName}{(Target is { Id: > 0 } ? $" -> {Target}" : "")}"
    };
}

public class ReplayObservations
{
    [JsonPropertyName("damage")] public List<LogDamage> Damage { get; set; } = new();
    [JsonPropertyName("auras")] public List<LogAura> Auras { get; set; } = new();
    [JsonPropertyName("resources")] public List<LogResourceSnapshot> Resources { get; set; } = new();
    [JsonPropertyName("channels")] public List<ReplayCommand> Channels { get; set; } = new();
}

public class LogDamage
{
    [JsonPropertyName("t")] public double Time { get; set; }
    [JsonPropertyName("ability_id")] public int AbilityId { get; set; }
    [JsonPropertyName("ability_name")] public string AbilityName { get; set; } = string.Empty;
    [JsonPropertyName("target")] public UnitRef Target { get; set; } = new(0, 1);
    [JsonPropertyName("amount")] public double Amount { get; set; }
    [JsonPropertyName("absorbed")] public double Absorbed { get; set; }
    [JsonPropertyName("overkill")] public double Overkill { get; set; }
    [JsonPropertyName("crit")] public bool Crit { get; set; }
    /// <summary>The log's hit type (<see cref="ReplayScriptBuilder.HitTypeCrit"/>, <see cref="ReplayScriptBuilder.HitTypeGrievousCrit"/>).</summary>
    [JsonPropertyName("hit_type")] public int? HitType { get; set; }
    [JsonPropertyName("tick")] public bool Tick { get; set; }

    /// <summary>Damage the hit dealt before absorbs.</summary>
    public double TotalAmount => Amount + Absorbed;
}

public class LogAura
{
    [JsonPropertyName("t")] public double Time { get; set; }
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("ability_id")] public int AbilityId { get; set; }
    [JsonPropertyName("ability_name")] public string AbilityName { get; set; } = string.Empty;
    [JsonPropertyName("source")] public UnitRef Source { get; set; } = new(0, 1);
    [JsonPropertyName("target")] public UnitRef Target { get; set; } = new(0, 1);
    [JsonPropertyName("stack")] public int? Stack { get; set; }
    [JsonPropertyName("duration_ms")] public double? DurationMs { get; set; }

    public bool IsGain => Type is "applybuff" or "applybuffstack" or "applydebuff" or "applydebuffstack" or "refreshbuff" or "refreshdebuff";
    public bool IsLoss => Type is "removebuff" or "removebuffstack" or "removedebuff" or "removedebuffstack";
}

public class LogResourceSnapshot
{
    [JsonPropertyName("t")] public double Time { get; set; }
    [JsonPropertyName("event")] public string Event { get; set; } = string.Empty;
    [JsonPropertyName("ability_id")] public int? AbilityId { get; set; }
    [JsonPropertyName("resources")] public Dictionary<string, LogResourceValue> Resources { get; set; } = new();
}

public class LogResourceValue
{
    [JsonPropertyName("amount")] public double Amount { get; set; }
    [JsonPropertyName("max")] public double Max { get; set; }
}
