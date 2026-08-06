namespace SanguoSLG.Core.Spatial;

/// <summary>
/// 타일의 부서짐 여부. 게임 진행(전투·약탈·재해 등)에 따라 바뀌며,
/// 표현 계층이 이 값을 보고 구조물을 무너뜨린다.
///
/// <b>형태만</b> 다룬다 — 색 변화와 화염·연기 같은 연출은 이 값과 직교하는 별개의
/// "효과"이며 효과 단계에서 따로 정의한다. 발동 규칙(어떤 상황에서 부서지는지)도 미정.
/// </summary>
public enum TileCondition
{
    /// <summary>정상.</summary>
    Normal,

    /// <summary>부서짐 — 지붕이 삐뚤어지고 건물 일부가 네모 형태만 남은 상태.</summary>
    Damaged,
}
