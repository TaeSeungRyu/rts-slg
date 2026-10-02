namespace SanguoSLG.Core.Data;

using System.Text.Json;
using SanguoSLG.Core.Domain;

public sealed class FieldBuildingLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public IReadOnlyList<FieldBuildingDefinition> LoadFromDirectory(string dataDirectory)
        => LoadFromJson(File.ReadAllText(Path.Combine(dataDirectory, "field-buildings.json")));

    public IReadOnlyList<FieldBuildingDefinition> LoadFromJson(string json)
    {
        var records = JsonSerializer.Deserialize<List<FieldBuildingDto>>(json, Options)
            ?? throw new InvalidDataException("야전 건축물 데이터를 역직렬화할 수 없습니다.");
        if (records.Count == 0)
        {
            throw new InvalidDataException("야전 건축물 정의가 비어 있습니다.");
        }

        var duplicate = records.GroupBy(x => x.Code, StringComparer.Ordinal)
            .FirstOrDefault(x => string.IsNullOrWhiteSpace(x.Key) || x.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidDataException($"야전 건축물 코드가 비어 있거나 중복됩니다: {duplicate.Key}");
        }

        return records.Select(Map).ToList();
    }

    private static FieldBuildingDefinition Map(FieldBuildingDto dto)
    {
        var kind = dto.Code switch
        {
            "palisade" => FieldBuildingKind.Palisade,
            "scout_post" => FieldBuildingKind.ScoutPost,
            "watchtower" => FieldBuildingKind.Watchtower,
            "fort" => FieldBuildingKind.Fort,
            "formation" => FieldBuildingKind.Formation,
            _ => throw new InvalidDataException($"알 수 없는 야전 건축물 코드: {dto.Code}"),
        };
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.GoldCost < 0 || dto.TroopCost < 0
            || dto.ProvisionsCost < 0 || dto.MaxHitPoints < 0 || dto.Defense < 0
            || dto.BuildDays <= 0 || dto.EffectRadius is < 1 or > 2
            || string.IsNullOrWhiteSpace(dto.ModelCode))
        {
            throw new InvalidDataException($"야전 건축물 정의 값이 올바르지 않습니다: {dto.Code}");
        }
        if (dto.CanBeTargeted && (dto.MaxHitPoints == 0 || dto.Defense == 0))
        {
            throw new InvalidDataException($"공격 가능한 건축물은 HP와 DF가 필요합니다: {dto.Code}");
        }
        if (!dto.CanBeTargeted && dto.MaxHitPoints != 0)
        {
            throw new InvalidDataException($"공격 불가 건축물은 HP를 가질 수 없습니다: {dto.Code}");
        }

        return new FieldBuildingDefinition(dto.Code, dto.Name, kind, dto.GoldCost, dto.TroopCost,
            dto.ProvisionsCost, dto.MaxHitPoints, dto.Defense, dto.BuildDays, dto.EffectRadius,
            dto.ModelCode, dto.CanBeTargeted, dto.CanGarrison);
    }

    private sealed class FieldBuildingDto
    {
        public string Code { get; init; } = "";
        public string Name { get; init; } = "";
        public int GoldCost { get; init; }
        public int TroopCost { get; init; }
        public int ProvisionsCost { get; init; }
        public int MaxHitPoints { get; init; }
        public int Defense { get; init; }
        public int BuildDays { get; init; }
        public int EffectRadius { get; init; }
        public string ModelCode { get; init; } = "";
        public bool CanBeTargeted { get; init; } = true;
        public bool CanGarrison { get; init; }
    }
}
