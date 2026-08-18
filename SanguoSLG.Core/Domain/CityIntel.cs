namespace SanguoSLG.Core.Domain;

/// <summary>
/// 정찰 성과 — 이 세력이 이 도시를 정찰했다(design-stratagem "도시 계략" 정찰). 정찰된 도시에만
/// 나머지 도시 계략(이간 등)·등용을 걸 수 있다. 1차는 영구 지속(2026-08-18 확정 — 만료는 후속 ❓).
/// 불변 값 — GameState의 별도 목록.
/// </summary>
public sealed record CityIntel(FactionId Faction, CityId City);
