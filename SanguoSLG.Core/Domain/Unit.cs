namespace SanguoSLG.Core.Domain;

using SanguoSLG.Core.Spatial;

/// <summary>
/// 맵 위를 이동하는 부대(불변). 위치 변경은 명시적 메서드를 통해서만 이뤄진다.
/// 스켈레톤 단계에서는 이동력·병력 등은 없이 위치와 소속만 가진다.
/// </summary>
public sealed record Unit(UnitId Id, FactionId Owner, HexCoord Position)
{
    /// <summary>지정한 좌표로 이동한 새 부대를 반환한다.</summary>
    public Unit MoveTo(HexCoord position) => this with { Position = position };
}
