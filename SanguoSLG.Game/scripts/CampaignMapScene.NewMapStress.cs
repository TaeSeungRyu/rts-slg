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
