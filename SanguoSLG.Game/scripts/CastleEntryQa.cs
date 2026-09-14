using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

public partial class CastleEntryQa : Node
{
    public override void _Ready()
    {
        try
        {
            var count = 0;
            foreach (var kind in new[] { "battle", "supply", "transport" })
            foreach (var distance in Enumerable.Range(2, 9))
            foreach (var speed in Enumerable.Range(1, 3))
            {
                Check(kind, distance, speed);
                count++;
            }
            GD.Print($"CASTLE_ENTRY_QA PASS: {count} campaign/animation cases");
            var exits = 0;
            foreach (var size in new[] { CastleSize.Small, CastleSize.Medium, CastleSize.Large })
            foreach (var direction in new HexCoord(0, 0).Neighbors())
            foreach (var kind in new[] { "battle", "supply", "transport", "army_group" })
            foreach (var speed in new[] { 1, 2, 3 })
            {
                CheckExit(size, direction, kind, speed);
                exits++;
            }
            GD.Print($"CASTLE_EXIT_QA PASS: {exits} campaign/animation cases");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private static void Check(string kind, int distance, int speed)
    {
        var field = new FieldUnit(new UnitId(1), new FactionId(1), new HexCoord(0, 0),
            speed, 0, 1, MovementDomain.Land, UnitMode.March, new HexCoord(distance, 0), 0);
        var unit = new CombatUnit(field, new CombatStats(1000, 1, 1), new TroopPool(1000, 0),
            UnitCombatState.Create(0), TroopCode: kind == "battle" ? "swordsman" : kind,
            IsSupply: kind == "supply", IsTransport: kind == "transport");
        var city = new City(new CityId(1), "도착", field.Target!.Value, field.Owner, 1000);
        var state = new GameState(1, 1, [], [city], [], FieldArmies: [unit]);
        var sim = new MovementSimulator(new PassabilityMap(new HexMap(-2, 20, -3, 3), [], []));
        var engine = new CampaignEngine(new AdvanceOrchestrator(sim,
            new CombatPhaseResolver(new BattleResolver(60), 70)), new WorldEngine(new BalanceConfig(100)));
        engine.AdvanceWeek(state, out var turns);
        var scene = new CampaignMapScene();
        try
        {
            typeof(CampaignMapScene).GetMethod("BuildAnimation", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(scene, [new Dictionary<int, HexCoord> { [1] = field.Position }, turns, Array.Empty<SiegeExchange>(), state]);
            var moves = Read<List<(double Time, int UnitId, HexCoord To)>>(scene, "_animSteps");
            var kills = Read<List<(double Time, int UnitId)>>(scene, "_animKills");
            var effects = Read<List<(double Time, Vector3 Position)>>(scene, "_animDeathEffects");
            var expectedMoves = Math.Min(distance - 1, 7 * speed);
            if (moves.Count != expectedMoves || moves.Any(m => m.UnitId != 1))
                throw new InvalidOperationException($"{kind}/{distance}/{speed}: missing moves {moves.Count}/{expectedMoves}");
            var previous = field.Position;
            foreach (var move in moves)
            {
                if (previous.Distance(move.To) != 1) throw new InvalidOperationException("Non-adjacent animation step");
                previous = move.To;
            }
            var entered = distance - 1 <= speed * 7;
            if (kills.Count != (entered ? 1 : 0) || effects.Count != 0)
                throw new InvalidOperationException("Premature/duplicate removal or death effect on entry");
            if (entered && (previous.Distance(city.Position) != 1
                || Math.Abs(kills[0].Time - (moves[^1].Time + 0.55)) > 0.00001))
                throw new InvalidOperationException("Entry removal is not synchronized with final movement");
        }
        finally { scene.Free(); }
    }

    private static void CheckExit(CastleSize size, HexCoord direction, string kind, int speed)
    {
        var city = new City(new CityId(1), "장안", new HexCoord(1, 2), new FactionId(1), 3000, size);
        var footprint = CastleFootprint.TilesFor(city).ToHashSet();
        var target = city.Position + direction;
        while (footprint.Contains(target)) target += direction;
        var field = new FieldUnit(new UnitId(1), city.Owner, city.Position, speed, 0, 1,
            MovementDomain.Land, UnitMode.Advance, target, 0);
        var unit = new CombatUnit(field, new CombatStats(1000, 1, 1), new TroopPool(1000, 0),
            UnitCombatState.Create(0), TroopCode: kind == "battle" ? "swordsman" : kind,
            IsSupply: kind == "supply", IsTransport: kind == "transport");
        var state = new GameState(1, 1, [], [city], [], FieldArmies: [unit]);
        var passability = new PassabilityMap(new HexMap(-10, 15, -10, 15), [], [city]);
        var engine = new CampaignEngine(new AdvanceOrchestrator(new MovementSimulator(passability),
            new CombatPhaseResolver(new BattleResolver(60), 70)), new WorldEngine(new BalanceConfig(100)));
        engine.AdvanceWeek(state, out var turns);
        var scene = new CampaignMapScene();
        try
        {
            typeof(CampaignMapScene).GetMethod("BuildAnimation", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(scene, [new Dictionary<int, HexCoord> { [1] = city.Position }, turns, Array.Empty<SiegeExchange>(), state]);
            var moves = Read<List<(double Time, int UnitId, HexCoord To)>>(scene, "_animSteps");
            var previous = city.Position;
            foreach (var move in moves)
            {
                if (move.To != previous + direction)
                    throw new InvalidOperationException($"{size}/{direction}/{kind}/{speed}: bent exit {previous} -> {move.To}");
                previous = move.To;
            }
            if (previous != target || moves.Count != city.Position.Distance(target)
                || Read<List<(double Time, int UnitId)>>(scene, "_animKills").Count != 0)
                throw new InvalidOperationException($"{size}/{direction}/{kind}/{speed}: incomplete exit or removed unit");
        }
        finally { scene.Free(); }
    }

    private static T Read<T>(object instance, string name) =>
        (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
}
