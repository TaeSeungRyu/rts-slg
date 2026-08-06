namespace SanguoSLG.Core.Spatial;

/// <summary>
/// 타일의 파괴 <b>형태</b> 단계. 게임 진행(전투·약탈·재해 등)에 따라 바뀌며,
/// 표현 계층이 이 값을 보고 구조물을 무너뜨린다.
///
/// 형태만 다룬다 — 화염·연기 같은 연출은 이 상태와 직교하는 별개의 "효과"이며
/// 효과 단계에서 따로 정의한다. 어떤 상황에서 어떤 단계로 전이되는지(발동 규칙)도 미정.
/// </summary>
public enum TileCondition
{
    /// <summary>정상.</summary>
    Normal,

    /// <summary>황폐 — 지붕이 내려앉고 담이 군데군데 끊긴 상태.</summary>
    Ruined,

    /// <summary>파괴 — 구조물이 크게 무너지고 잔해가 널린 상태.</summary>
    Destroyed,
}
