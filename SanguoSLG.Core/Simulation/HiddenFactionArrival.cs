namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>시나리오에 등록된 지정 연도 등장 세력의 예약. 등장 후 상태에서 제거한다.</summary>
public sealed record HiddenFactionArrival(Faction Faction, int Year, CityId PreferredCity,
    IReadOnlyList<GeneralId> Generals,
    IReadOnlyList<HiddenFactionCitySetup>? CitySetups = null,
    IReadOnlyList<GeneralPosting>? GeneralPostings = null);

/// <summary>등장 전에는 지도에서 숨기고, 등장 시 복원할 거점별 사전 설정.</summary>
public sealed record HiddenFactionCitySetup(CityId City, int Gold, int Provisions, int Security,
    IReadOnlyList<GarrisonForce> Garrisons, IReadOnlyList<PortShipStock> Ships);
