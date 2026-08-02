namespace SanguoSLG.Core.Domain;

using SanguoSLG.Core.Spatial;

/// <summary>
/// 도시. 헥사 맵 위의 한 지점을 차지하며 특정 세력이 소유한다.
/// 상태 변경은 명시적 메서드(with 식)를 통해서만 이뤄진다.
/// </summary>
public sealed record City(
    CityId Id,
    string Name,
    HexCoord Position,
    FactionId Owner,
    int Provisions)
{
    /// <summary>소유 세력을 바꾼 새 도시를 반환한다.</summary>
    public City WithOwner(FactionId owner) => this with { Owner = owner };
}
