namespace SanguoSLG.Core.Spatial;

/// <summary>
/// 타일의 파괴 상태. 게임 진행(전투·약탈·재해 등)에 따라 바뀌며, 표현 계층이 이 값을 보고 효과를 입힌다.
/// 어떤 상황에서 어떤 상태로 전이되는지(규칙)는 아직 정의하지 않았다 — 여기서는 표현만 정의한다.
/// </summary>
public enum TileCondition
{
    /// <summary>정상.</summary>
    Normal,

    /// <summary>황폐 — 색이 바래고 구조물 일부가 무너진 상태.</summary>
    Ruined,

    /// <summary>불타는 중 — 황폐보다 심하게 그을리고 화염·연기가 인다.</summary>
    Burning,
}
