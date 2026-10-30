namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>시나리오에 등록된 숨은세력의 출현 예약. 출현 후 상태에서 제거한다.</summary>
public sealed record HiddenFactionArrival(Faction Faction, int Year, CityId PreferredCity,
    IReadOnlyList<GeneralId> Generals);
