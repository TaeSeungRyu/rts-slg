namespace SanguoSLG.Core.Domain;

/// <summary>
/// 정찰 성과 — 이 세력이 이 도시를 정찰했다(design-stratagem "도시 계략" 정찰). 정찰된 도시에만
/// 나머지 도시 계략·등용을 걸 수 있다. 정찰은 성공일부터 60일 동안 유지된다.
/// 불변 값 — GameState의 별도 목록.
/// </summary>
public sealed record CityIntel(FactionId Faction, CityId City, int ExpiresDay = int.MaxValue);
