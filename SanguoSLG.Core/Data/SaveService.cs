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
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize(GameState state) => JsonSerializer.Serialize(state, Options);

    public static GameState Deserialize(string json)
        => JsonSerializer.Deserialize<GameState>(json, Options)
           ?? throw new InvalidDataException("세이브 데이터를 역직렬화할 수 없습니다.");

    public static void Save(GameState state, string path) => File.WriteAllText(path, Serialize(state));

    public static GameState Load(string path) => Deserialize(File.ReadAllText(path));
}
