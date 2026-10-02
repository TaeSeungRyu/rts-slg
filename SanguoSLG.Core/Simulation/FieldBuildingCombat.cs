namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public sealed record FieldBuildingExchange(FieldBuildingId Building, UnitId Attacker, int Damage, bool Destroyed);

public sealed record FieldBuildingCombatResult(GameState State, IReadOnlyList<CombatUnit> Armies,
    IReadOnlyList<FieldBuildingExchange> Exchanges);

/// <summary>적 야전 건축물을 도시형 고정 표적으로 공격한다. 건축물은 반격하지 않는다.</summary>
public sealed class FieldBuildingCombat
{
    private readonly BattleResolver _battle;
    private readonly IReadOnlyDictionary<string, FieldBuildingDefinition> _definitions;

    public FieldBuildingCombat(BattleResolver battle, IReadOnlyList<FieldBuildingDefinition> definitions)
    {
        _battle = battle;
        _definitions = definitions.ToDictionary(x => x.Code, StringComparer.Ordinal);
    }

    public FieldBuildingCombatResult Resolve(GameState state, IReadOnlyList<CombatUnit> input)
    {
        var armies = input.ToList();
        var buildings = state.Buildings.ToDictionary(x => x.Id);
        var exchanges = new List<FieldBuildingExchange>();
        foreach (var original in state.Buildings.OrderBy(x => x.Id.Value))
        {
            if (!buildings.TryGetValue(original.Id, out var building)
                || !_definitions.TryGetValue(building.DefinitionCode, out var definition)
                || !definition.CanBeTargeted) continue;
            foreach (var attacker in armies.OrderBy(x => x.Field.CommandOrder).ThenBy(x => x.Id.Value))
            {
                if (attacker.Field.Owner == building.Owner || !attacker.CanInitiateCombat || attacker.Pool.Active <= 0
                    || attacker.Field.Target != building.Position
                    || attacker.Field.Position.Distance(building.Position) > attacker.Field.RangeCastle) continue;
                var target = new CombatStats(System.Math.Max(1, building.HitPoints), 0,
                    definition.Defense, AptitudeGrade.C.Percent());
                var damage = System.Math.Min(building.HitPoints,
                    _battle.Damage(attacker.Stats with { Troops = attacker.Pool.Active }, target));
                if (damage <= 0) continue;
                var hp = building.HitPoints - damage;
                exchanges.Add(new(building.Id, attacker.Id, damage, hp <= 0));
                if (hp <= 0) { buildings.Remove(building.Id); break; }
                building = building with { HitPoints = hp };
                buildings[building.Id] = building;
            }
        }
        var aliveBuilders = buildings.Values.Where(x => x.BuilderUnit is not null).Select(x => x.BuilderUnit!.Value).ToHashSet();
        var released = armies.Select(x => x.IsConstructing && !aliveBuilders.Contains(x.Id)
            ? x with { IsConstructing = false, Field = x.Field with { Target = null, Waypoints = null } }
            : x).ToList();
        var ordered = state.Buildings.Where(x => buildings.ContainsKey(x.Id)).Select(x => buildings[x.Id]).ToList();
        return new(state with { FieldBuildings = ordered, FieldArmies = released }, released, exchanges);
    }
}
