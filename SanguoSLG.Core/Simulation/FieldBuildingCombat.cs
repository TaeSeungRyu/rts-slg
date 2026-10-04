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
    private readonly IReadOnlyDictionary<string, TroopTemplate> _troops;

    public FieldBuildingCombat(BattleResolver battle, IReadOnlyList<FieldBuildingDefinition> definitions,
        IReadOnlyList<TroopTemplate>? troops = null)
    {
        _battle = battle;
        _definitions = definitions.ToDictionary(x => x.Code, StringComparer.Ordinal);
        _troops = (troops ?? []).ToDictionary(x => x.Code, StringComparer.Ordinal);
    }

    public FieldBuildingCombatResult Resolve(GameState state, IReadOnlyList<CombatUnit> input)
    {
        var armies = input.ToList();
        var buildings = state.Buildings.ToDictionary(x => x.Id);
        var exchanges = new List<FieldBuildingExchange>();
        var attacked = new HashSet<UnitId>();
        foreach (var original in state.Buildings.OrderBy(x => x.Id.Value))
        {
            if (!buildings.TryGetValue(original.Id, out var building)
                || !_definitions.TryGetValue(building.DefinitionCode, out var definition)
                || !definition.CanBeTargeted) continue;
            foreach (var attacker in armies.OrderBy(x => x.Field.CommandOrder).ThenBy(x => x.Id.Value))
            {
                if (attacked.Contains(attacker.Id) || attacker.IsWaitingDeployment
                    || attacker.Field.Mode == UnitMode.March || attacker.State.Statuses.Any(s => s.IsDaze)
                    || attacker.Field.Owner == building.Owner || !attacker.CanInitiateCombat || attacker.Pool.Active <= 0
                    || attacker.Field.Position.Distance(building.Position) > attacker.Field.RangeCastle) continue;
                var preferred = state.Buildings.Where(b => buildings.ContainsKey(b.Id) && b.Owner != attacker.Field.Owner
                        && _definitions.TryGetValue(b.DefinitionCode, out var d) && d.CanBeTargeted
                        && attacker.Field.Position.Distance(b.Position) <= attacker.Field.RangeCastle)
                    .OrderBy(b => b.Position == attacker.Field.Target ? 0 : 1)
                    .ThenBy(b => attacker.Field.Position.Distance(b.Position)).ThenBy(b => b.Id.Value).FirstOrDefault();
                if (preferred?.Id != building.Id) continue;
                var defenseBonus = BuildingDefenseBonus(state, building, armies);
                var target = new CombatStats(System.Math.Max(1, building.HitPoints), 0,
                    definition.Defense, AptitudeGrade.APlus.Percent(), DfBonusPercent: defenseBonus);
                var buildingAttack = BuildingAttack(attacker);
                var damage = System.Math.Min(building.HitPoints,
                    _battle.Damage(attacker.Stats with
                    {
                        Troops = attacker.Pool.Active,
                        AtkStat = buildingAttack,
                    }, target));
                if (damage <= 0) continue;
                attacked.Add(attacker.Id);
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

    private int BuildingAttack(CombatUnit attacker)
    {
        if (attacker.IsSupply)
            return System.Math.Max(1, _troops.Values.Select(t => t.AtkBuilding).DefaultIfEmpty(1).Min());
        if (attacker.IsArmyGroup) return 6;
        return _troops.TryGetValue(attacker.TroopCode, out var troop)
            ? troop.AtkBuilding
            : attacker.Stats.AtkStat; // 구형 저장/단위 테스트 호환
    }

    private static int BuildingDefenseBonus(GameState state, FieldBuilding building,
        IReadOnlyList<CombatUnit> armies)
    {
        if (building.GarrisonUnit is not { } garrisonId) return 100;
        var garrison = armies.FirstOrDefault(unit => unit.Id == garrisonId && unit.Pool.Active > 0);
        if (garrison is null) return 100;
        var passiveBonus = System.Math.Max(100, garrison.Stats.DfBonusPercent);
        var defenseAptitude = garrison.VanguardId is { } generalId
            ? state.Generals.FirstOrDefault(general => general.Id == generalId)?.AptitudeFor(TroopClass.Defense).Percent() ?? 100
            : 100;
        // 낮은 수성 적성은 기본 건축물보다 약하게 만들지 않고, A+ 초과부터 보너스로 적용한다.
        return passiveBonus * System.Math.Max(100, defenseAptitude) / 100;
    }
}
