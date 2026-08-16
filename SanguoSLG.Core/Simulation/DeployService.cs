namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

/// <summary>출전 요청 — 도시 대기 병력(병종)과 선봉(+부관)을 야전 부대로 편성한다.</summary>
/// <param name="Troops">데려갈 병력(대기 병력에서 차감). 0 이하면 그 병종 전량.</param>
public sealed record DeployRequest(
    CityId City,
    string TroopCode,
    int Troops,
    GeneralId Vanguard,
    GeneralId? Adjutant = null,
    UnitMode Mode = UnitMode.March,
    HexCoord? Target = null);

/// <summary>
/// 출전(design-administration "부대와의 연결"·design-unit-state). 대기 병력 + 장수 → 야전 부대:
/// 군량을 성 비축에서 떼어 휴대(적재 상한 = 한 달치), 훈련도 승계, 장수는 야전으로(Location null).
/// 부대는 성 타일에서 시작해 다음 진행의 게이트 스텝으로 걸어 나간다. 즉시 실행(기간 없음) —
/// 진행 전 명령 페이즈의 행동이다. 징병 부대는 훈련도가 최소치(50) 미만이면 투입 불가.
/// </summary>
public sealed class DeployService
{
    private readonly CommandBalance _b;
    private readonly IReadOnlyDictionary<string, TroopTemplate> _troops;
    private readonly IReadOnlyDictionary<string, ActiveSkill> _actives;
    private readonly IReadOnlyDictionary<string, PassiveSkill> _passives;

    // 패시브 버킷은 조립 시점에 접힌다(현 구조) — 야전 상시 조건으로 평가한다.
    private static readonly CombatContext FieldContext = new(MeleeEngagement: true, IncomingMelee: true, InField: true);

    public DeployService(CommandBalance balance, IReadOnlyList<TroopTemplate> troops,
        IReadOnlyList<ActiveSkill> actives, IReadOnlyList<PassiveSkill> passives)
    {
        _b = balance;
        _troops = troops.ToDictionary(t => t.Code);
        _actives = actives.ToDictionary(a => a.Code);
        _passives = passives.ToDictionary(p => p.Code);
    }

    public CommandResult Deploy(GameState state, DeployRequest req)
    {
        var city = state.Cities.FirstOrDefault(c => c.Id == req.City);
        if (city is null)
        {
            return CommandResult.Fail("도시를 찾을 수 없다.", state);
        }

        if (!_troops.TryGetValue(req.TroopCode, out var template))
        {
            return CommandResult.Fail("병종을 지정해야 한다.", state);
        }

        var garrison = state.Garrisons.FirstOrDefault(g => g.City == req.City && g.TroopCode == req.TroopCode);
        if (garrison is null || garrison.Troops <= 0)
        {
            return CommandResult.Fail("그 병종의 대기 병력이 없다.", state);
        }

        var troops = req.Troops <= 0 ? garrison.Troops : req.Troops;
        if (troops > garrison.Troops)
        {
            return CommandResult.Fail("대기 병력이 부족하다.", state);
        }

        if (garrison.TrainingLevel < _b.DeployMinTraining)
        {
            return CommandResult.Fail($"훈련도 {_b.DeployMinTraining} 미만 부대는 투입할 수 없다(징병 훈련 중).", state);
        }

        var vanguard = state.Generals.FirstOrDefault(g => g.Id == req.Vanguard);
        if (vanguard is null)
        {
            return CommandResult.Fail("선봉 장수를 찾을 수 없다.", state);
        }

        General? adjutant = null;
        if (req.Adjutant is { } adjId)
        {
            if (adjId == req.Vanguard)
            {
                return CommandResult.Fail("부관은 선봉과 달라야 한다.", state);
            }

            adjutant = state.Generals.FirstOrDefault(g => g.Id == adjId);
            if (adjutant is null)
            {
                return CommandResult.Fail("부관 장수를 찾을 수 없다.", state);
            }
        }

        foreach (var generalId in new[] { req.Vanguard, req.Adjutant }.OfType<GeneralId>())
        {
            if (state.IsGeneralBusy(generalId))
            {
                return CommandResult.Fail("장수가 내정 명령에 매여 있다.", state);
            }

            if (state.Assignments.Count > 0)
            {
                var posting = state.PostingOf(generalId);
                if (posting is null || posting.Faction != city.Owner || posting.Location != city.Id)
                {
                    return CommandResult.Fail("이 도시에 주둔 중인 소속 장수만 출전할 수 있다.", state);
                }
            }
        }

        // 군량 휴대: 적재 상한(한 달치 × 병력 비례)과 성 비축 중 작은 쪽.
        var capacity = template.ProvisionsCapacity * troops / 10000;
        var carried = System.Math.Min(capacity, city.Provisions);

        var unitId = new UnitId(state.Armies.Count == 0 ? 1 : state.Armies.Max(u => u.Id.Value) + 1);
        var unit = UnitAssembler.Assemble(unitId, city.Owner, city.Position, req.Mode, req.Target,
            unitId.Value, vanguard, adjutant, template, troops, _actives, _passives, FieldContext);
        unit = unit with { Provisions = carried, Training = garrison.TrainingLevel };

        var garrisons = state.Garrisons
            .Select(g => g == garrison ? g with { Troops = g.Troops - troops } : g)
            .Where(g => g.Troops > 0)
            .ToList();
        var cities = state.Cities
            .Select(c => c.Id == city.Id ? c with { Provisions = c.Provisions - carried } : c)
            .ToList();
        var postings = state.Assignments
            .Select(p => p.General == req.Vanguard || (req.Adjutant is { } a && p.General == a)
                ? p with { Location = null }
                : p)
            .ToList();

        return CommandResult.Success(state with
        {
            Cities = cities,
            GarrisonForces = garrisons,
            Postings = postings,
            FieldArmies = state.Armies.Append(unit).ToList(),
        });
    }
}
