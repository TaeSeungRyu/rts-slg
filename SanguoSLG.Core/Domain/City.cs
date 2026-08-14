namespace SanguoSLG.Core.Domain;

using SanguoSLG.Core.Spatial;

/// <summary>
/// 도시. 헥사 맵 위의 한 지점을 차지하며 특정 세력이 소유한다.
/// 상태 변경은 명시적 메서드(with 식)를 통해서만 이뤄진다.
/// 금은 도시별 소유(2026-08-13 확정 — 수송·약탈이 전략 요소), 광석·말·코끼리는
/// 병력 생산 자원 비축(design-administration "생산 자원과 시장"), Region은 지역 코드(regions.json).
/// 시설(논·밭·마을·공방)은 개수로만 둔다 — 타일 위치는 표현(Game) 계층 몫이고,
/// 컬렉션을 넣으면 record 값 동등성이 깨져 결정론 검증이 무너진다.
/// </summary>
public sealed record City(
    CityId Id,
    string Name,
    HexCoord Position,
    FactionId Owner,
    int Provisions,
    CastleSize Castle = CastleSize.Small,
    int Gold = 0,
    int Security = 100,
    int Population = 0,
    int Ore = 0,
    int Horses = 0,
    int Elephants = 0,
    string Region = "",
    int Paddies = 0,
    int Farms = 0,
    int Villages = 0,
    bool Workshop = false,
    bool ProducesOre = false,
    bool ProducesHorses = false,
    bool ProducesElephants = false)
{
    /// <summary>소유 세력을 바꾼 새 도시를 반환한다.</summary>
    public City WithOwner(FactionId owner) => this with { Owner = owner };

    /// <summary>금을 더한(음수면 뺀) 새 도시를 반환한다.</summary>
    public City AddGold(int amount) => this with { Gold = Gold + amount };
}
