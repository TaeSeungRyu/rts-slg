namespace SanguoSLG.Core.Data;

using System.Text.Json;
using SanguoSLG.Core.Simulation;

/// <summary>data/command-balance.json을 읽어 명령 밸런스를 만든다. 엔진 비의존.</summary>
public sealed class CommandBalanceLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public CommandBalance LoadFromDirectory(string dataDirectory)
        => LoadFromJson(File.ReadAllText(Path.Combine(dataDirectory, "command-balance.json")));

    public CommandBalance LoadFromJson(string json)
        => JsonSerializer.Deserialize<CommandBalance>(json, Options)
            ?? throw new InvalidDataException("명령 밸런스 데이터를 역직렬화할 수 없습니다.");
}
