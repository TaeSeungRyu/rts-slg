using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Simulation.RenewalMovement;
using SanguoSLG.Core.Spatial;
using System;
using System.Linq;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private HexCoord? _newMapBattleCenter;

    private void SeedNewMapBattleUnits()
    {
        var enemy = _state.Factions.First(faction => faction.Id != Player).Id;
        var cityPositions = _state.Cities.Select(city => city.Position).ToArray();
        var occupied = _state.Armies.Select(unit => unit.Field.Position).ToHashSet();
        var origin = _state.Cities.First(city => city.Owner == Player).Position;
        var anchor = _map.Tiles()
            .Where(hex => hex.Distance(origin) < 30)
            .OrderBy(hex => hex.Distance(origin))
            .First(hex => Enumerable.Range(0, 10).All(row =>
                Enumerable.Range(0, 7).All(column =>
                {
                    var tile = new HexCoord(hex.Q + column, hex.R + row);
                    return _map.Contains(tile)
                        && TerrainRules.CanEnter(MovementDomain.Land, _map.TerrainAt(tile))
                        && cityPositions.All(city => city.Distance(tile) > 2)
                        && !occupied.Contains(tile);
                })));
        _newMapBattleCenter = new HexCoord(anchor.Q + 3, anchor.R + 5);
        var troop = _troops.First(type => type.Class == TroopClass.Infantry);
        var skills = _activeSkills.Where(skill => skill.Type == ActiveType.Strike).ToArray();
        if (skills.Length == 0) skills = _activeSkills.ToArray();
        var units = new List<CombatUnit>(60);
        for (var side = 0; side < 2; side++)
        {
            var owner = side == 0 ? Player : enemy;
            var officers = _state.Assignments.Where(posting => posting.Faction == owner)
                .Select(posting => _state.Generals.FirstOrDefault(general => general.Id == posting.General))
                .Where(general => general is not null).ToArray();
            for (var index = 0; index < 30; index++)
            {
                var row = index / 3;
                var column = index % 3 + (side == 0 ? 0 : 4);
                var position = new HexCoord(anchor.Q + column, anchor.R + row);
                var target = new HexCoord(anchor.Q + (side == 0 ? 4 : 2), anchor.R + row);
                var officer = officers.Length == 0 ? null : officers[index % officers.Length];
                var skill = officer?.BattleActive is { } code
                    ? _activeSkills.FirstOrDefault(active => active.Code == code)
                    : null;
                skill ??= skills[index % skills.Length];
                var state = UnitCombatState.Create(officer?.Intellect ?? 70, skill) with
                {
                    VanguardGauge = new ActiveGauge(4),
                    AdjutantGauge = new ActiveGauge(4),
                };
                var field = new FieldUnit(new UnitId(910000 + side * 30 + index), owner, position,
                    2, 3, 1, MovementDomain.Land, UnitMode.Attack, target, 0,
                    ContinuousTarget: RenewalHexSpace.Center(target));
                units.Add(new CombatUnit(field, new CombatStats(10000, troop.AtkUnit, troop.Df),
                    new TroopPool(10000, 0), state, officer?.Might ?? 70, officer?.Intellect ?? 70,
                    MaxTroops: 10000, Class: troop.Class, Provisions: 1000,
                    ProvisionsCapacity: 1000, TroopCode: troop.Code, VanguardId: officer?.Id,
                    RenewalPosition: RenewalHexSpace.Center(position)));
            }
        }
        _state = _state with { FieldArmies = _state.Armies.Concat(units).ToList() };
        GD.Print($"[newmap-battle60] player=30 enemy=30 active={units.Count(unit => unit.State.VanguardActive is not null)} center={_newMapBattleCenter}");
    }

    private void RunNewMapBattleQa()
    {
        var units = _state.Armies.Where(unit => unit.Id.Value is >= 910000 and < 910060).ToArray();
        var passed = units.Length == 60 && units.Count(unit => unit.Field.Owner == Player) == 30
            && units.Count(unit => unit.Field.Owner != Player) == 30
            && units.All(unit => unit.State.VanguardActive is not null && unit.State.VanguardGauge.ElapsedDays == 4);
        if (passed)
        {
            StartAdvance();
            passed = _animActives.Any(active => active.UnitId is >= 910000 and < 910060);
        }
        GD.Print($"[newmap-battle60-qa] passed={passed} units={units.Length} activeFires={_animActives.Count(active => active.UnitId is >= 910000 and < 910060)}");
        GetTree().Quit(passed ? 0 : 1);
    }

    // Temporary, opt-in movement load test: run-newmap.bat stress100.
    private void SeedNewMapStressUnits()
    {
        var startCity = _state.Cities.First(city => city.Owner == Player);
        var infantry = _troops.First(troop => troop.Class == TroopClass.Infantry);
        var cityPositions = _state.Cities.Select(city => city.Position).ToArray();
        var positions = _map.Tiles()
            .Where(hex => TerrainRules.CanEnter(MovementDomain.Land, _map.TerrainAt(hex)))
            .Where(hex => cityPositions.All(city => hex.Distance(city) > 2))
            .OrderBy(hex => hex.Distance(startCity.Position))
            .ThenBy(hex => hex.Q).ThenBy(hex => hex.R)
            .Take(100).ToArray();
        if (positions.Length != 100)
            throw new InvalidOperationException("100부대 테스트를 배치할 육상 타일이 부족합니다.");

        var units = positions.Select((position, index) =>
        {
            var destination = positions[(index + 50) % positions.Length];
            var field = new FieldUnit(new UnitId(900000 + index), Player, position,
                2, 2, 1, MovementDomain.Land, UnitMode.March, destination, 0,
                ContinuousTarget: RenewalHexSpace.Center(destination));
            return new CombatUnit(field,
                new CombatStats(1000, infantry.AtkUnit, infantry.Df),
                new TroopPool(1000, 0), UnitCombatState.Create(70),
                MaxTroops: 1000, Class: infantry.Class, Provisions: 500,
                ProvisionsCapacity: 500, TroopCode: infantry.Code,
                RenewalPosition: RenewalHexSpace.Center(position));
        }).ToArray();
        _state = _state with { FieldArmies = _state.Armies.Concat(units).ToList() };
        GD.Print($"[newmap-stress100] seeded={units.Length} moving={units.Count(unit => unit.Field.Target is not null)} "
            + $"destination-distance={units.Min(unit => unit.Field.Position.Distance(unit.Field.Target!.Value))}.."
            + $"{units.Max(unit => unit.Field.Position.Distance(unit.Field.Target!.Value))}");
    }

    private void RunNewMapStressAdvanceQa()
    {
        var before = _state.Armies
            .Where(unit => unit.Id.Value is >= 900000 and < 900100)
            .ToDictionary(unit => unit.Id, unit => unit.RenewalPosition!.Value);
        StartAdvance();
        var after = _pendingState.Armies
            .Where(unit => before.ContainsKey(unit.Id))
            .ToArray();
        var moved = after.Count(unit => unit.RenewalPosition is { } position
            && position != before[unit.Id]);
        var passed = before.Count == 100 && moved > 0;
        GD.Print($"[newmap-stress100-qa] passed={passed} initial={before.Count} survived={after.Length} moved={moved}");
        GetTree().Quit(passed ? 0 : 1);
    }
}
