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
/// 시설 포위 처리(design-administration "시설 파괴·약탈"·10e-B).
/// 현재 규칙에서는 시설을 파괴하지 않는다. 적 도시를 포위하더라도 논·밭·마을·공방·자원시설은
/// 사라지거나 잔해로 바뀌지 않으며, 생산 UI도 유지된다. 노획은 별도 규칙이 생길 때까지 발생하지 않는다.
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

    // 시설은 파괴되지 않는다. 약탈/노획 재설계 전까지는 보고 없이 그대로 둔다.
    private PlunderReport? Plunder(City city, CombatUnit looter, out City? plundered, out CombatUnit? afterLooter)
    {
        plundered = null;
        afterLooter = null;
        return null;
    }
}
