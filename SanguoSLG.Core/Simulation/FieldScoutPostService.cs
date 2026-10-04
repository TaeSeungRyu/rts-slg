namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

public sealed record ScoutPostUpdate(GameState State, IReadOnlyList<CombatUnit> Armies,
    IReadOnlyList<FieldBuildingId> Removed, IReadOnlyList<FieldBuildingId> Expired);

/// <summary>정찰대의 60일 만료와 적 통과 철수를 일 단위로 정산한다.</summary>
public sealed class FieldScoutPostService
{
    private readonly IReadOnlySet<string> _scoutCodes;

    public FieldScoutPostService(IReadOnlyList<FieldBuildingDefinition> definitions)
    {
        _scoutCodes = definitions.Where(x => x.Kind == FieldBuildingKind.ScoutPost)
            .Select(x => x.Code).ToHashSet(StringComparer.Ordinal);
    }

    public ScoutPostUpdate Resolve(GameState state, IReadOnlyList<CombatUnit> armies,
        IReadOnlyDictionary<FactionId, IReadOnlySet<HexCoord>> visitedByFaction, int day)
    {
        var expired = state.Buildings.Where(x => IsScout(x) && x.IsExpired(day)).Select(x => x.Id).ToHashSet();
        // 정찰대는 매복 관측소라 소속을 불문하고 어떤 부대든 해당 타일을 통과하면
        // 노출되어 즉시 철수한다. 건설 중 부대는 visited 경로에 포함되지 않는다.
        var intruded = state.Buildings.Where(x => IsScout(x) && !expired.Contains(x.Id)
            && visitedByFaction.Values.Any(visited => visited.Contains(x.Position)))
            .Select(x => x.Id).ToHashSet();
        var removed = expired.Concat(intruded).ToHashSet();
        if (removed.Count == 0)
            return new(state, armies, [], []);

        var removedBuilders = state.Buildings.Where(x => removed.Contains(x.Id) && x.BuilderUnit is not null)
            .Select(x => x.BuilderUnit!.Value).ToHashSet();
        var released = armies.Select(unit => removedBuilders.Contains(unit.Id)
            ? unit with
            {
                IsConstructing = false,
                Field = unit.Field with { Mode = UnitMode.Advance, Target = null, Waypoints = null },
            }
            : unit).ToList();
        var releasedById = released.ToDictionary(x => x.Id);
        var next = state with
        {
            FieldArmies = state.Armies.Select(x => releasedById.GetValueOrDefault(x.Id, x)).ToList(),
            FieldBuildings = state.Buildings.Where(x => !removed.Contains(x.Id)).ToList(),
        };
        return new(next, released, intruded.OrderBy(x => x.Value).ToList(), expired.OrderBy(x => x.Value).ToList());
    }

    private bool IsScout(FieldBuilding building) => _scoutCodes.Contains(building.DefinitionCode);
}
