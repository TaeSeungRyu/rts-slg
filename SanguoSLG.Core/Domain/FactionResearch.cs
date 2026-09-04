namespace SanguoSLG.Core.Domain;

/// <summary>
/// 세력 단위 병종 연구 트랙(design-combat "병종 연구"·11단계 확정 2026-08-17). 세력이 병종별로
/// 독립 트랙 하나(0~10단계)를 올리며, 그 세력의 그 병종 부대 공/방에 flat 보정(ResearchCurve)이
/// 붙는다. v2에서는 공방 보유 없이 진행할 수 있다. 불변 값 — GameState의 별도 목록으로 둔다(결정론).
/// </summary>
public sealed record FactionResearch(FactionId Faction, string TroopCode, int Level)
{
    /// <summary>성벽 연구 트랙의 예약 코드(병종 코드와 겹치지 않음) — 세력 단위 성벽 최대값 5단계.</summary>
    public const string WallCode = "__wall__";
}
