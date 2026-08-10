namespace SanguoSLG.Game;

/// <summary>
/// 효과가 붙을 수 있는 대상 범위(doc/design-effect.md "적용 대상 제약").
/// 대부분은 <see cref="Both"/>이지만 일부는 유닛/건물 한쪽에만 쓴다.
/// </summary>
public enum EffectTargetScope
{
    /// <summary>유닛·건물 모두에 붙을 수 있다.</summary>
    Both,

    /// <summary>유닛 전용(예: 대상 메시를 조각내는 파괴 연출).</summary>
    Unit,

    /// <summary>건물 전용.</summary>
    Building,
}
