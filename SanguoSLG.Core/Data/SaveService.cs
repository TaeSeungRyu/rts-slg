namespace SanguoSLG.Core.Data;

using System.Text.Json;
using System.Text.Json.Serialization;
using SanguoSLG.Core.Simulation;

/// <summary>
/// 게임 저장/불러오기 — <see cref="GameState"/>를 JSON으로 왕복한다(System.Text.Json).
/// 결정론·순수 데이터라 상태만 담으면 되고, 로더로 만든 정적 데이터(스킬·병종 등)는 저장하지 않는다.
/// </summary>
public static class SaveService
{
    public const int CurrentSchemaVersion = 2;
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize(GameState state)
        => JsonSerializer.Serialize(new SaveEnvelope(CurrentSchemaVersion, state), Options);

    public static GameState Deserialize(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("SchemaVersion", out _)
            || document.RootElement.TryGetProperty("schemaVersion", out _))
        {
            var envelope = JsonSerializer.Deserialize<SaveEnvelope>(json, Options)
                ?? throw new InvalidDataException("세이브 데이터를 역직렬화할 수 없습니다.");
            if (envelope.SchemaVersion is < 1 or > CurrentSchemaVersion)
            {
                throw new InvalidDataException($"지원하지 않는 세이브 스키마: {envelope.SchemaVersion}");
            }
            return envelope.State;
        }

        // 18B 이전 원시 GameState JSON과의 호환.
        return JsonSerializer.Deserialize<GameState>(json, Options)
            ?? throw new InvalidDataException("세이브 데이터를 역직렬화할 수 없습니다.");
    }

    public static void Save(GameState state, string path) => File.WriteAllText(path, Serialize(state));

    public static GameState Load(string path) => Deserialize(File.ReadAllText(path));
}

public sealed record SaveEnvelope(int SchemaVersion, GameState State);
