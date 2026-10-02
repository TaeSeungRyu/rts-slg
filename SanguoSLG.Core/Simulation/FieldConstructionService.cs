namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

public sealed record FieldConstructionRequest(UnitId Unit, string DefinitionCode, HexCoord Position);

public sealed class FieldConstructionService(
    IReadOnlyList<FieldBuildingDefinition> definitions,
    Func<HexCoord, TerrainType> terrainAt)
{
    public const int MinimumTroops = 1000;

    private readonly IReadOnlyDictionary<string, FieldBuildingDefinition> _definitions =
        definitions.ToDictionary(x => x.Code, StringComparer.Ordinal);

    public CommandResult Start(GameState state, FactionId faction, FieldConstructionRequest request)
    {
        var unit = state.Armies.FirstOrDefault(x => x.Id == request.Unit && x.Field.Owner == faction);
        if (unit is null || unit.Pool.Active <= 0)
        {
            return CommandResult.Fail("건축할 수 있는 아군 부대가 없습니다.", state);
        }
        if (unit.Class == TroopClass.Naval)
        {
            return CommandResult.Fail("해상 부대는 건축할 수 없습니다.", state);
        }
        if (unit.IsWaitingDeployment)
        {
            return CommandResult.Fail("출격 대기 중인 부대는 건축할 수 없습니다.", state);
        }
        if (IsConstructing(state, unit.Id))
        {
            return CommandResult.Fail("이미 건축 중인 부대입니다.", state);
        }
        if (!_definitions.TryGetValue(request.DefinitionCode, out var definition))
        {
            return CommandResult.Fail("알 수 없는 건축물입니다.", state);
        }
        if (unit.Pool.Active < MinimumTroops)
        {
            return CommandResult.Fail("건축에는 병력 1,000명 이상이 필요합니다.", state);
        }
        if (unit.Field.Position.Distance(request.Position) > 1)
        {
            return CommandResult.Fail("현재 위치 또는 인접한 타일에만 건축할 수 있습니다.", state);
        }
        if (!CanBuildOn(definition.Kind, terrainAt(request.Position)))
        {
            return CommandResult.Fail("이 지형에는 해당 건축물을 설치할 수 없습니다.", state);
        }
        if (IsOccupied(state, request.Position))
        {
            return CommandResult.Fail("이미 점유되거나 건축이 예약된 타일입니다.", state);
        }
        if (unit.CarryingGold < definition.GoldCost)
        {
            return CommandResult.Fail("부대가 휴대한 금이 부족합니다.", state);
        }
        if (!unit.TracksProvisions || unit.Provisions < definition.ProvisionsCost)
        {
            return CommandResult.Fail("부대가 휴대한 군량이 부족합니다.", state);
        }
        if (unit.Pool.Active - definition.TroopCost <= 0)
        {
            return CommandResult.Fail("건축에 투입할 병력이 부족합니다.", state);
        }

        var paid = Pay(unit, definition);
        var nextId = new FieldBuildingId(state.Buildings.Select(x => x.Id.Value).DefaultIfEmpty(0).Max() + 1);
        var completionDay = state.Day + definition.BuildDays;
        int? expiresDay = definition.LifetimeDays > 0 ? completionDay + definition.LifetimeDays : null;
        var building = new FieldBuilding(nextId, definition.Code, faction, request.Position,
            definition.CanBeTargeted ? System.Math.Max(1, definition.MaxHitPoints / 2) : 0,
            state.Day, completionDay, expiresDay, unit.Id);

        return CommandResult.Success(state with
        {
            FieldArmies = state.Armies.Select(x => x.Id == unit.Id ? paid : x).ToList(),
            FieldBuildings = state.Buildings.Append(building).ToList(),
        });
    }

    public static bool IsConstructing(GameState state, UnitId unit)
        => state.Armies.Any(x => x.Id == unit && x.IsConstructing)
            || state.Buildings.Any(x => x.BuilderUnit == unit && !x.IsCompleted(state.Day));

    public static IReadOnlySet<UnitId> ConstructionUnits(GameState state, int day)
        => state.Buildings.Where(x => x.BuilderUnit is not null && !x.IsCompleted(day))
            .Select(x => x.BuilderUnit!.Value).ToHashSet();

    public static IReadOnlyList<CombatUnit> ReleaseCompleted(
        IReadOnlyList<CombatUnit> units, IReadOnlyList<FieldBuilding> buildings, int day)
    {
        var completed = buildings.Where(x => x.BuilderUnit is not null && x.IsCompleted(day))
            .Select(x => x.BuilderUnit!.Value).ToHashSet();
        return units.Select(x => completed.Contains(x.Id)
            ? x with
            {
                IsConstructing = false,
                Field = x.Field with { Mode = UnitMode.Advance, Target = null, Waypoints = null },
            }
            : x).ToList();
    }

    private static bool CanBuildOn(FieldBuildingKind kind, TerrainType terrain)
        => kind == FieldBuildingKind.ScoutPost
            ? terrain is TerrainType.Forest or TerrainType.Mountain
            : terrain is TerrainType.Plains or TerrainType.Forest or TerrainType.Desert;

    private static bool IsOccupied(GameState state, HexCoord position)
        => state.Buildings.Any(x => x.Position == position)
            || state.Placements.Any(x => x.Plot == position)
            || state.Ruins.Any(x => x.Position == position)
            || state.Cities.Any(x => CastleFootprint.TilesFor(x).Contains(position));

    private static CombatUnit Pay(CombatUnit unit, FieldBuildingDefinition definition)
    {
        var cargoPaid = System.Math.Min(unit.CargoGold, definition.GoldCost);
        var lootPaid = definition.GoldCost - cargoPaid;
        return unit with
        {
            CargoGold = unit.CargoGold - cargoPaid,
            LootGold = unit.LootGold - lootPaid,
            Provisions = unit.Provisions - definition.ProvisionsCost,
            Pool = new TroopPool(unit.Pool.Active - definition.TroopCost, unit.Pool.Wounded),
            IsConstructing = true,
            Field = unit.Field with { Mode = UnitMode.Advance, Target = null, Waypoints = null },
        };
    }
}
