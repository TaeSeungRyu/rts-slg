namespace SanguoSLG.Core.Domain;

/// <summary>
/// 세력 단위 병종 연구 트랙(design-combat "병종 연구"·11단계 확정 2026-08-17). 세력이 병종별로
/// 독립 트랙 하나(0~10단계)를 올리며, 그 세력의 그 병종 부대 공/방에 flat 보정(ResearchCurve)이
/// 붙는다. 공방 보유가 진행 게이트. 불변 값 — GameState의 별도 목록으로 둔다(결정론).
/// </summary>
public sealed record FactionResearch(FactionId Faction, string TroopCode, int Level);
