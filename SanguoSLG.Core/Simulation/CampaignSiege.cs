namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

/// <summary>한 도시에 대한 이번 공성 교환 보고(캠페인 레벨).</summary>
/// <param name="City">공성당한 도시.</param>
/// <param name="WallStanding">교환 시작 시 성벽이 서 있었는가.</param>
/// <param name="WallDamage">성벽에 흡수된 피해.</param>
/// <param name="NewWall">교환 후 남은 성벽.</param>
/// <param name="TroopDamage">수비 병력 손실(성벽 초과분·붕괴 직격).</param>
/// <param name="Besiegers">공성에 가담한 부대(입력 순서).</param>
/// <param name="BesiegerDamage">부대별 성 반격 피해(<paramref name="Besiegers"/>와 같은 순서). 표현 계층 연출용.</param>
/// <param name="TurnIndex">이 교환이 속한 진행 조각(AdvanceTurn) 인덱스 — 재생 타이밍용(-1 = 미지정).</param>
public sealed record SiegeExchange(
    CityId City,
    bool WallStanding,
    int WallDamage,
    int NewWall,
    int TroopDamage,
    IReadOnlyList<UnitId> Besiegers,
    IReadOnlyList<int>? BesiegerDamage = null,
    int TurnIndex = -1);

/// <summary>
/// 캠페인 공성(design-combat "성 전투"). 이동이 성 접적으로 멈춘 뒤 한 진행마다 1회 교환: 성벽을
/// 두들기고(붕괴 시 수비 병력 직격), 성이 사거리 1 공격 부대에 반격한다(투석·공성탑 등 사거리 밖은
/// 반격 없음). 수비는 도시 대기 병력(GarrisonForce) 총합이며 손실은 병력 비례로 병종에 분배된다.
/// **소유 전환·함락은 다음 단계** — 이 단계는 성벽·수비를 깎는 데까지다. 결정론: 도시·부대 id 순.
/// </summary>
public sealed class CampaignSiege
{
    private readonly BattleResolver _resolver;
    private readonly IReadOnlyDictionary<string, TroopTemplate> _troops;
    private readonly int _woundedPercent;
    private readonly Func<HexCoord, TerrainType> _terrainAt;

    /// <summary>성 반격 유닛dmg·성벽 df·붕괴 후 df(design-combat 기본치).</summary>
    private const int CastleUnitDmg = 10;
    private const int WallDf = 12;
    private const int CollapsedDf = 6;

    public CampaignSiege(BattleResolver resolver, IReadOnlyList<TroopTemplate> troops,
        int woundedPercent = 70, Func<HexCoord, TerrainType>? terrainAt = null)
    {
        _resolver = resolver;
        _troops = troops.ToDictionary(t => t.Code);
        _woundedPercent = woundedPercent;
        _terrainAt = terrainAt ?? (_ => TerrainType.Plains);
    }

    public sealed record Result(
        IReadOnlyList<CombatUnit> Armies,
        IReadOnlyList<City> Cities,
        IReadOnlyList<GarrisonForce> Garrisons,
        IReadOnlyList<SiegeExchange> Exchanges);

    /// <summary>이번 진행의 공성 교환을 전부 정산한 새 상태를 반환한다.</summary>
    /// <param name="counterAptitude">도시별 성 반격 위력 퍼센트(태수 무력 연동). null이면 전부 100%.</param>
    public Result Resolve(IReadOnlyList<CombatUnit> armies, IReadOnlyList<City> cities,
        IReadOnlyList<GarrisonForce> garrisons, Func<CityId, int>? counterAptitude = null)
    {
        var byUnit = armies.ToDictionary(u => u.Id);
        var cityById = cities.ToDictionary(c => c.Id);
        var garr = garrisons.ToList();
        var exchanges = new List<SiegeExchange>();

        foreach (var city in cities.OrderBy(c => c.Id.Value))
        {
            var defenders = garr.Where(g => g.City == city.Id).OrderBy(g => g.TroopCode, StringComparer.Ordinal).ThenBy(g => g.Trainee).ToList();
            var defendTroops = defenders.Sum(g => g.Troops);
            if (city.Wall <= 0 && defendTroops <= 0)
            {
                continue; // 이미 무너지고 빈 성 — 함락(점거)은 다음 단계
            }

            var besiegers = armies
                .Where(u => u.Pool.Active > 0 && u.Field.Owner != city.Owner
                    && u.Field.Mode == UnitMode.Attack && !u.IsSupply && u.TroopCode.Length > 0
                    && _troops.ContainsKey(u.TroopCode)
                    && u.Field.Position.Distance(city.Position) <= _troops[u.TroopCode].RangeCastle)
                .OrderBy(u => u.Id.Value)
                .ToList();
            if (besiegers.Count == 0)
            {
                continue;
            }

            var counterPercent = counterAptitude?.Invoke(city.Id) ?? 100;
            var castle = new CastleState(city.Wall, defendTroops, CastleUnitDmg, WallDf, CollapsedDf, counterPercent);
            var attackers = besiegers.Select(u => BuildAttacker(u, city.Position)).ToList();
            var outcome = _resolver.ResolveSiege(attackers, castle);

            cityById[city.Id] = city with { Wall = outcome.NewWall };
            if (outcome.TroopDamage > 0 && defendTroops > 0)
            {
                DistributeDefenderLoss(garr, defenders, outcome.TroopDamage, defendTroops);
            }

            var counters = new List<int>(besiegers.Count);
            for (var i = 0; i < besiegers.Count; i++)
            {
                var counter = outcome.CounterDamage[i];
                counters.Add(counter);
                if (counter <= 0)
                {
                    continue;
                }

                var u = byUnit[besiegers[i].Id];
                byUnit[u.Id] = u with { Pool = u.Pool.TakeDamage(counter, _woundedPercent) };
            }

            exchanges.Add(new SiegeExchange(city.Id, outcome.WallStanding, outcome.WallDamage,
                outcome.NewWall, outcome.TroopDamage, besiegers.Select(u => u.Id).ToList(), counters));
        }

        return new Result(
            armies.Select(u => byUnit[u.Id]).ToList(),
            cities.Select(c => cityById[c.Id]).ToList(),
            garr.Where(g => g.Troops > 0).ToList(),
            exchanges);
    }

    // 수비 병력 손실을 병종별 대기 병력에 병력 비례로 분배한다(잔여는 병종 코드 순 1씩 — 결정론).
    private static void DistributeDefenderLoss(List<GarrisonForce> garr, IReadOnlyList<GarrisonForce> defenders,
        int totalLoss, int total)
    {
        var loss = Math.Min(totalLoss, total);
        var applied = 0;
        var shares = defenders.Select(d => (Force: d, Share: (int)((long)loss * d.Troops / total))).ToList();
        applied = shares.Sum(s => s.Share);
        var remainder = loss - applied;
        for (var i = 0; remainder > 0 && i < shares.Count; i++)
        {
            if (shares[i].Force.Troops - shares[i].Share > 0)
            {
                shares[i] = (shares[i].Force, shares[i].Share + 1);
                remainder--;
            }
        }

        foreach (var (force, share) in shares)
        {
            var idx = garr.IndexOf(force);
            garr[idx] = force with { Troops = force.Troops - share };
        }

        garr.RemoveAll(g => g.Troops <= 0);
    }

    private SiegeAttacker BuildAttacker(CombatUnit u, HexCoord castlePos)
    {
        var template = _troops[u.TroopCode];
        var (terrainAtk, _) = TerrainCombatBonus.For(template.Class, _terrainAt(u.Field.Position));
        var inCounterRange = u.Field.Position.Distance(castlePos) <= 1;
        return new SiegeAttacker(
            u.Pool.Active,
            template.AtkBuilding + terrainAtk,
            u.Stats.AtkStat,
            u.Stats.DfStat,
            u.Stats.AptitudePercent,
            u.Stats.AtkBonusPercent,
            u.Stats.DfBonusPercent,
            inCounterRange);
    }
}
