namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

/// <summary>생산 작전 발행 서비스. 성 대기 병력 500명과 장수 1명을 시설로 파견한다.</summary>
public sealed class ProductionService
{
    private readonly IReadOnlyDictionary<string, TroopTemplate> _troops;
    private readonly Func<HexCoord, bool> _canEnter;

    public ProductionService(IReadOnlyList<TroopTemplate> troops, Func<HexCoord, bool>? canEnter = null)
    {
        _troops = troops.ToDictionary(t => t.Code);
        _canEnter = canEnter ?? (_ => true);
    }

    public CommandResult Start(GameState state, CityId cityId, HexCoord target, string facility,
        string troopCode, GeneralId generalId)
    {
        var city = state.Cities.FirstOrDefault(c => c.Id == cityId);
        if (city is null) { return CommandResult.Fail("도시를 찾을 수 없다.", state); }
        if (!ProductionRules.IsProductionFacility(facility)) { return CommandResult.Fail("생산 가능한 시설이 아니다.", state); }
        if (!_troops.TryGetValue(troopCode, out var troop)) { return CommandResult.Fail("병종을 지정해야 한다.", state); }
        if (troop.Class == TroopClass.Naval) { return CommandResult.Fail("해상 병종은 생산 작전에 투입할 수 없다.", state); }

        var placement = state.Placements.FirstOrDefault(p => p.City == cityId && p.Plot == target && p.Code == facility);
        if (placement is null) { return CommandResult.Fail("해당 위치에 생산 시설이 없다.", state); }
        if (state.ProductionOps.Any(p => p.Target == target && p.Phase != ProductionPhase.Returning))
        {
            return CommandResult.Fail("이미 해당 시설에서 생산 작전이 진행 중이다.", state);
        }

        var garrison = state.Garrisons.FirstOrDefault(g => g.City == cityId && g.TroopCode == troopCode && !g.Trainee);
        if (garrison is null || garrison.Troops < ProductionOperation.FixedTroops)
        {
            return CommandResult.Fail("생산 작전에는 대기 병력 500명이 필요하다.", state);
        }

        var general = state.Generals.FirstOrDefault(g => g.Id == generalId);
        if (general is null) { return CommandResult.Fail("장수를 찾을 수 없다.", state); }
        if (state.IsGeneralBusy(generalId) || state.IsGeneralInField(generalId))
        {
            return CommandResult.Fail("장수가 다른 임무를 수행 중이다.", state);
        }

        if (state.Assignments.Count > 0)
        {
            var posting = state.PostingOf(generalId);
            if (posting is null || posting.Faction != city.Owner || posting.Location != city.Id)
            {
                return CommandResult.Fail("이 도시에 주둔 중인 소속 장수만 생산 작전에 투입할 수 있다.", state);
            }
        }

        var path = new HexPathfinder(c => c == city.Position || c == target || _canEnter(c))
            .FindPath(city.Position, target);
        if (path.Count < 2) { return CommandResult.Fail("생산 시설까지 이동할 수 없다.", state); }

        var opId = state.ProductionOps.Count == 0 ? 1 : state.ProductionOps.Max(o => o.Id) + 1;
        var op = new ProductionOperation(opId, city.Id, city.Owner, city.Position, target, facility,
            troopCode, ProductionOperation.FixedTroops, generalId, state.Day, ProductionRules.GatherDays(general.Politics),
            PhaseStartedDay: state.Day, Position: city.Position, OutboundPath: path);
        var garrisons = state.Garrisons
            .Select(g => g == garrison ? g with { Troops = g.Troops - ProductionOperation.FixedTroops } : g)
            .Where(g => g.Troops > 0)
            .ToList();
        var postings = state.Assignments
            .Select(p => p.General == generalId ? p with { Location = null } : p)
            .ToList();

        return CommandResult.Success(state with
        {
            GarrisonForces = garrisons,
            Postings = postings,
            ProductionOperations = state.ProductionOps.Append(op).ToList(),
        });
    }
}
