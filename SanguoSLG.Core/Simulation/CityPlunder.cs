namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>한 도시에 대한 이번 약탈 보고.</summary>
/// <param name="City">약탈당한 도시.</param>
/// <param name="Facility">파괴된 시설("village"/"paddy"/"farm"/"workshop").</param>
/// <param name="Looter">노획을 받은 부대.</param>
/// <param name="Gold">노획한 금.</param>
/// <param name="Provisions">노획한 군량(휴대 한도 내 — 초과분은 소실).</param>
public sealed record PlunderReport(CityId City, string Facility, UnitId Looter, int Gold, int Provisions);

/// <summary>
/// 시설 파괴·약탈(design-administration "시설 파괴·약탈"·10e-B). 적 도시를 포위(인접 1칸·공격모드)한
/// 부대가 있으면 매 진행 그 도시의 시설 1개를 파괴하고 건설 비용의 노획률(50%)만큼 노획한다 —
/// 논·밭은 군량으로(부대 휴대 한도까지, 초과 소실=불태움), 마을·공방은 금으로(무제한 휴대).
/// 파괴 우선순위: 마을 → 논 → 밭 → 공방 → 자원 시설(광산·목장·상원 — 파괴 플래그, 금 200 노획).
/// 일반 시설은 잔해(Ruined*)로 남아 수리로 복구할 수 있다. 노획은 최저 id 포위 부대가 받는다.
/// 결정론: 도시·부대 id 순, 난수 없음.
/// </summary>
public sealed class CityPlunder
{
    private readonly CommandBalance _b;

    public CityPlunder(CommandBalance balance) => _b = balance;

    public sealed record Result(
        IReadOnlyList<CombatUnit> Armies,
        IReadOnlyList<City> Cities,
        IReadOnlyList<PlunderReport> Reports);

    /// <summary>이번 진행의 약탈을 전부 정산한 새 상태를 반환한다(도시당 시설 1개).</summary>
    public Result Resolve(IReadOnlyList<CombatUnit> armies, IReadOnlyList<City> cities)
    {
        var byUnit = armies.ToDictionary(u => u.Id);
        var cityById = cities.ToDictionary(c => c.Id);
        var reports = new List<PlunderReport>();

        foreach (var city in cities.OrderBy(c => c.Id.Value))
        {
            var current = cityById[city.Id];
            var looter = armies
                .Where(u => u.Pool.Active > 0 && u.Field.Owner != current.Owner
                    && u.Field.Mode == UnitMode.Attack && !u.IsSupply
                    && u.Field.Position.Distance(current.Position) <= 1)
                .OrderBy(u => u.Id.Value)
                .Select(u => byUnit[u.Id])
                .FirstOrDefault();
            if (looter is null)
            {
                continue;
            }

            var report = Plunder(current, looter, out var plundered, out var afterLooter);
            if (report is null)
            {
                continue; // 부술 시설이 없다
            }

            cityById[city.Id] = plundered!;
            byUnit[looter.Id] = afterLooter!;
            reports.Add(report);
        }

        return new Result(
            armies.Select(u => byUnit[u.Id]).ToList(),
            cities.Select(c => cityById[c.Id]).ToList(),
            reports);
    }

    // 우선순위(마을→논→밭→공방)대로 시설 1개를 부수고 노획을 부대에 싣는다. 없으면 null.
    private PlunderReport? Plunder(City city, CombatUnit looter, out City? plundered, out CombatUnit? afterLooter)
    {
        plundered = null;
        afterLooter = null;

        string facility;
        var gold = 0;
        var provisions = 0;
        if (city.Villages > 0)
        {
            facility = "village";
            gold = Loot(_b.BuildCostVillage);
            plundered = city with { Villages = city.Villages - 1, RuinedVillages = city.RuinedVillages + 1 };
        }
        else if (city.Paddies > 0)
        {
            facility = "paddy";
            provisions = Loot(_b.BuildCostPaddy);
            plundered = city with { Paddies = city.Paddies - 1, RuinedPaddies = city.RuinedPaddies + 1 };
        }
        else if (city.Farms > 0)
        {
            facility = "farm";
            provisions = Loot(_b.BuildCostFarm);
            plundered = city with { Farms = city.Farms - 1, RuinedFarms = city.RuinedFarms + 1 };
        }
        else if (city.Workshop)
        {
            facility = "workshop";
            gold = Loot(_b.BuildCostWorkshop);
            plundered = city with { Workshop = false, WorkshopRuined = true };
        }
        else if (city.ProducesOre && !city.MineDestroyed)
        {
            facility = "mine";
            gold = ResourceFacilityLoot;
            plundered = city with { MineDestroyed = true };
        }
        else if (city.ProducesHorses && !city.RanchDestroyed)
        {
            facility = "ranch";
            gold = ResourceFacilityLoot;
            plundered = city with { RanchDestroyed = true };
        }
        else if (city.ProducesElephants && !city.ElephantGardenDestroyed)
        {
            facility = "elephant_garden";
            gold = ResourceFacilityLoot;
            plundered = city with { ElephantGardenDestroyed = true };
        }
        else
        {
            return null;
        }

        // 군량 노획은 부대 휴대 최대치까지만(초과분은 소실 — 불태운 것). 금은 무제한.
        if (provisions > 0 && looter.TracksProvisions)
        {
            provisions = System.Math.Min(provisions, looter.MaxProvisions() - looter.Provisions);
            provisions = System.Math.Max(0, provisions);
        }
        else if (provisions > 0)
        {
            provisions = 0; // 군량 미추적 부대(하베스트 등)는 노획 군량을 실을 곳이 없다
        }

        afterLooter = looter with
        {
            LootGold = looter.LootGold + gold,
            Provisions = provisions > 0 ? looter.Provisions + provisions : looter.Provisions,
        };
        return new PlunderReport(city.Id, facility, looter.Id, gold, provisions);
    }

    private int Loot(int buildCost) => buildCost * _b.PlunderPercent / 100;

    /// <summary>지역 고정 자원 시설(광산·목장·상원) 파괴 노획(금) — 수리 정액 400의 50%(2026-08-18 확정).</summary>
    private const int ResourceFacilityLoot = 200;
}
