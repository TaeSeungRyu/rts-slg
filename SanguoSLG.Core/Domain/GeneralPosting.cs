namespace SanguoSLG.Core.Domain;

/// <summary>
/// 장수 배속 — 어느 세력 소속이며 지금 어느 도시에 있는가(불변). 이것이 있어야 명령·출전·AI·등용이
/// "누구의 장수인가"를 안다. <see cref="Location"/>이 null이면 야전에 출전 중(부대 편성 — 후속).
/// 배속은 세이브 상태다(가변) — 등용·이동·함락으로 바뀐다. 참조 능력치는 <see cref="Domain.General"/>.
/// </summary>
public sealed record GeneralPosting(GeneralId General, FactionId Faction, CityId? Location);
