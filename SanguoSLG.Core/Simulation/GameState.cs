namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;

/// <summary>
/// 한 시점의 게임 전체 상태(불변). 시간은 **일(日) 단위 세계 시계** 하나로 흐른다
/// (2026-08-13 확정 — design-administration "시간 축"). 1개월 = 30일, 1년 = 360일로
/// 고정해 연·월·일은 절대 일수에서 유도한다. 상태 전이는 WorldEngine이 새 GameState를 만든다.
/// </summary>
public sealed record GameState(
    int Day,
    int StartYear,
    IReadOnlyList<Faction> Factions,
    IReadOnlyList<City> Cities,
    IReadOnlyList<General> Generals,
    IReadOnlyList<CityCommand>? PendingCommands = null,
    IReadOnlyList<GeneralPosting>? Postings = null,
    IReadOnlyList<GarrisonForce>? GarrisonForces = null,
    IReadOnlyList<CombatUnit>? FieldArmies = null,
    IReadOnlyList<Prisoner>? Captives = null,
    IReadOnlyList<FactionResearch>? ResearchTracks = null,
    IReadOnlyList<CityIntel>? ScoutedCities = null,
    int MarketPricePercent = 100,
    IReadOnlyList<FacilityPlacement>? FacilityPlacements = null)
{
    /// <summary>건설한 시설이 놓인 성 주변 타일(표현 계층이 모델을 얹는다). 건설 완료 시 append.</summary>
    public IReadOnlyList<FacilityPlacement> Placements => FacilityPlacements ?? [];

    /// <summary>도시 대기 병력(병종별) — 모집 정산이 쌓고, 출전 편성이 꺼내 쓴다.</summary>
    public IReadOnlyList<GarrisonForce> Garrisons => GarrisonForces ?? [];

    /// <summary>야전 부대 — 캠페인 진행(CampaignEngine)이 이동·전투를 굴린다.</summary>
    public IReadOnlyList<CombatUnit> Armies => FieldArmies ?? [];

    /// <summary>진행 중인 도시 명령(발행됨·미완료). 수행 장수는 완료까지 잠긴다.</summary>
    public IReadOnlyList<CityCommand> Commands => PendingCommands ?? [];

    /// <summary>장수 배속(소속 세력·주둔 도시).</summary>
    public IReadOnlyList<GeneralPosting> Assignments => Postings ?? [];

    /// <summary>포로 목록(억류 세력·원 세력). 함락·등용 실패로 생긴다(design-general-lifecycle §2).</summary>
    public IReadOnlyList<Prisoner> Prisoners => Captives ?? [];

    /// <summary>이 장수가 어느 세력의 포로인가(아니면 null).</summary>
    public Prisoner? PrisonerOf(GeneralId general)
        => Prisoners.FirstOrDefault(p => p.General == general);

    /// <summary>이 세력이 억류 중인 포로 목록.</summary>
    public IEnumerable<Prisoner> PrisonersHeldBy(FactionId holder)
        => Prisoners.Where(p => p.Holder == holder);

    /// <summary>이 세력이 소유한 도시 수(0이면 세력 소멸 대상 — design-general-lifecycle §3).</summary>
    public int CityCount(FactionId faction) => Cities.Count(c => c.Owner == faction);

    /// <summary>이 장수의 현재 충성도(재야·포로 포함 — roster 값을 그대로 읽는다).</summary>
    public int LoyaltyOf(GeneralId general)
        => Generals.FirstOrDefault(g => g.Id == general)?.Loyalty ?? 0;

    /// <summary>세력 병종 연구 트랙(design-combat "병종 연구").</summary>
    public IReadOnlyList<FactionResearch> Research => ResearchTracks ?? [];

    /// <summary>이 세력의 그 병종 연구 단계(없으면 0 = 미연구).</summary>
    public int ResearchOf(FactionId faction, string troopCode)
        => Research.FirstOrDefault(r => r.Faction == faction && r.TroopCode == troopCode)?.Level ?? 0;

    /// <summary>이 세력의 성벽 연구 단계(0=미연구 20% … 4=완료 100%).</summary>
    public int WallLevelOf(FactionId faction) => ResearchOf(faction, FactionResearch.WallCode);

    /// <summary>정찰 성과 목록(design-stratagem 정찰 계략).</summary>
    public IReadOnlyList<CityIntel> Intel => ScoutedCities ?? [];

    /// <summary>이 세력이 이 도시를 정찰했는가 — 나머지 도시 계략·등용의 전제.</summary>
    public bool IsScouted(FactionId faction, CityId city)
        => Intel.Any(i => i.Faction == faction && i.City == city);

    /// <summary>이 장수가 진행 중 명령에 매여 잠겨 있는가.</summary>
    public bool IsGeneralBusy(GeneralId general) => Commands.Any(c => c.Locks(general));

    /// <summary>이 장수의 배속(없으면 null — 재야).</summary>
    public GeneralPosting? PostingOf(GeneralId general)
        => Assignments.FirstOrDefault(p => p.General == general);

    /// <summary>이 도시에 주둔 중인 장수 목록(id).</summary>
    public IEnumerable<GeneralId> GeneralsAt(CityId city)
        => Assignments.Where(p => p.Location == city).Select(p => p.General);

    /// <summary>이 세력 소속 장수 목록(id).</summary>
    public IEnumerable<GeneralId> GeneralsOf(FactionId faction)
        => Assignments.Where(p => p.Faction == faction).Select(p => p.General);

    public const int DaysPerMonth = 30;
    public const int MonthsPerYear = 12;
    public const int DaysPerYear = DaysPerMonth * MonthsPerYear;

    public int Year => StartYear + (Day - 1) / DaysPerYear;

    public int Month => (Day - 1) % DaysPerYear / DaysPerMonth + 1;

    public int DayOfMonth => (Day - 1) % DaysPerMonth + 1;

    /// <summary>시나리오로부터 시작 상태(시작 연도 1월 1일)를 만든다.</summary>
    public static GameState FromScenario(Scenario scenario, int startYear = 1) =>
        new(1, startYear, scenario.Factions, scenario.Cities, scenario.Generals, Postings: scenario.Postings);
}
