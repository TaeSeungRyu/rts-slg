namespace SanguoSLG.Game;

/// <summary>
/// 범용 효과 카탈로그(doc/design-effect.md "효과 목록"). 식별자는 문서와 일치시킨다.
/// 아직 구현된 것만 <see cref="EffectView.Attach"/>가 처리하고, 나머지는 순차 구현한다.
/// </summary>
public enum EffectKind
{
    /// <summary>빨강색 불이 피어오르는 효과.</summary>
    Fire,
    Desaturate,
    Flies,
    Flood,
    Skulls,
    Daze,
    Smoke,
    Burst,
    Villagers,
    Clouds,
    Waterfall,
    Confusion,
}
