namespace SanguoSLG.Core.Data;

// JSON 역직렬화 전용 DTO. 도메인 타입(강타입 ID, HexCoord)은 JSON에 직접 노출하지 않고
// 여기서 원시 값으로 받은 뒤 ScenarioLoader가 도메인으로 매핑한다.
// snake_case 키는 JsonSerializerOptions의 명명 정책으로 처리한다.

internal sealed class FactionDto
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public int Ruler { get; init; }
    public int Gold { get; init; }
    public string Color { get; init; } = "#c02626";
}

internal sealed class CityDto
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public int Q { get; init; }
    public int R { get; init; }
    public int Owner { get; init; }
    public int Provisions { get; init; }

    // 성곽 등급: "small"(기본) | "medium" | "large"
    public string Castle { get; init; } = "small";
}

internal sealed class GeneralDto
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public int Leadership { get; init; }
    public int Might { get; init; }
    public int Intellect { get; init; }
    public int Politics { get; init; }
    public int Charisma { get; init; }
}

internal sealed class BalanceDto
{
    public int MonthlyTaxPerCity { get; init; }
}

internal sealed class MapDto
{
    public int MinQ { get; init; }
    public int MaxQ { get; init; }
    public int MinR { get; init; }
    public int MaxR { get; init; }
    public TerrainDto? Terrain { get; init; }
    public List<FeatureDto> Features { get; init; } = new();
    public List<ConditionDto> Conditions { get; init; } = new();
}

internal sealed class FeatureDto
{
    // 지물 종류: "mountain_medium"
    public string Type { get; init; } = "";
    public int Q { get; init; }
    public int R { get; init; }
}

internal sealed class ConditionDto
{
    // 파괴 상태: "ruined" | "burning" (정상은 기록하지 않는다)
    public string State { get; init; } = "";
    public int Q { get; init; }
    public int R { get; init; }
}

internal sealed class TerrainDto
{
    // 한 글자 코드 → 지형 이름(예: "G" → "plains")
    public Dictionary<string, string> Legend { get; init; } = new();

    // rows[i]는 r = min_r + i 행, 각 문자는 q = min_q + j 열의 지형 코드
    public List<string> Rows { get; init; } = new();
}
