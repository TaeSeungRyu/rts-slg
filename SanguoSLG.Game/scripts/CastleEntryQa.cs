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
            var queues = 0;
            foreach (var size in new[] { CastleSize.Small, CastleSize.Medium, CastleSize.Large })
            foreach (var direction in new HexCoord(0, 0).Neighbors())
            foreach (var unitCount in Enumerable.Range(2, 4))
            {
                CheckQueuedExit(size, direction, unitCount);
                queues++;
            }
            GD.Print($"CASTLE_EXIT_QUEUE_QA PASS: {queues} campaign/animation cases");
            CheckPlaybackDeploymentVisibility();
            CheckControlledDeployment();
            GD.Print("CONTROLLED_DEPLOYMENT_QA PASS: Changan SW/SE replay, adjacent steps, movement budget");
            GD.Print("CASTLE_EXIT_VISIBILITY_QA PASS: released deployment remains visible during first playback");
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
                .Invoke(scene, [new Dictionary<int, HexCoord> { [1] = field.Position }, turns,
                    Array.Empty<SiegeExchange>(), Array.Empty<CaptureReport>(), state]);
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

    private static void CheckControlledDeployment()
    {
        var city = new City(new CityId(1), "장안", new HexCoord(1, 2), new FactionId(1), 3000, CastleSize.Medium);
        var ruin = new HexCoord(0, 4);
        var map = (HexMap)typeof(CampaignMapScene).GetMethod("BuildTestMap", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;
        var passability = new PassabilityMap(map, [], [city], [ruin]);
        var units = new[] { DeploymentDirection.SouthWest, DeploymentDirection.SouthEast }.Select((direction, index) =>
        {
            var exit = DeploymentEgressRules.RepresentativeExit(city, direction, ruin,
                tile => passability.CanEnter(MovementDomain.Land, tile))!.Value;
            return new CombatUnit(new FieldUnit(new UnitId(index + 1), city.Owner, city.Position, index == 0 ? 3 : 2,
                0, 1, MovementDomain.Land, UnitMode.Attack, ruin, index), new CombatStats(10000, 1, 1),
                new TroopPool(10000, 0), UnitCombatState.Create(0), OriginCity: city.Id,
                EgressDirection: direction, EgressExit: exit, AwaitingEgress: true);
        }).ToArray();
        var state = new GameState(1, 1, [], [city], [], FieldArmies: units);
        var engine = new CampaignEngine(new AdvanceOrchestrator(new MovementSimulator(passability),
            new CombatPhaseResolver(new BattleResolver(60), 70), provisionsPer10kPerDay: 0), new WorldEngine(new BalanceConfig(100)));
        var after = engine.AdvanceWeek(state, out var turns);
        var scene = new CampaignMapScene();
        try
        {
            typeof(CampaignMapScene).GetField("_pendingState", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(scene, after);
            typeof(CampaignMapScene).GetMethod("BuildAnimation", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(scene, [units.ToDictionary(u => u.Id.Value, _ => city.Position), turns,
                    Array.Empty<SiegeExchange>(), Array.Empty<CaptureReport>(), state]);
            var moves = Read<List<(double Time, int UnitId, HexCoord To)>>(scene, "_animSteps");
            var starts = Read<Dictionary<int, HexCoord>>(scene, "_animStartOverrides");
            var deployments = Read<List<(double Time, int UnitId)>>(scene, "_animDeployments");
            if (deployments.Count != 2 || deployments.Any(d => d.Time != 0))
                throw new InvalidOperationException("Controlled release missing or duplicated");
            foreach (var unit in units)
            {
                var path = moves.Where(m => m.UnitId == unit.Id.Value).OrderBy(m => m.Time).ToArray();
                if (path.Length == 0 || path[0].To != unit.EgressExit)
                    throw new InvalidOperationException("Selected exit differs from first animation step");
                var previous = starts[unit.Id.Value];
                foreach (var step in path)
                {
                    if (previous.Distance(step.To) != 1)
                        throw new InvalidOperationException($"Non-adjacent controlled step {previous} -> {step.To}");
                    previous = step.To;
                }
                if (path.Count(m => m.Time < 2.5) > unit.Field.Speed)
                    throw new InvalidOperationException("Release exceeded first-day movement budget");
            }
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
                .Invoke(scene, [new Dictionary<int, HexCoord> { [1] = city.Position }, turns,
                    Array.Empty<SiegeExchange>(), Array.Empty<CaptureReport>(), state]);
            var moves = Read<List<(double Time, int UnitId, HexCoord To)>>(scene, "_animSteps");
            var starts = Read<Dictionary<int, HexCoord>>(scene, "_animStartOverrides");
            if (moves.Count != 1 || moves[0].To != target
                || Read<List<(double Time, int UnitId)>>(scene, "_animKills").Count != 0)
                throw new InvalidOperationException($"{size}/{direction}/{kind}/{speed}: incomplete exit or removed unit");
            var visualStart = starts.GetValueOrDefault(1, city.Position);
            if (visualStart.Distance(target) != 1)
                throw new InvalidOperationException($"{size}/{direction}/{kind}/{speed}: visual egress starts too far {visualStart} -> {target}");
        }
        finally { scene.Free(); }
    }

    private static void CheckQueuedExit(CastleSize size, HexCoord direction, int count)
    {
        var city = new City(new CityId(1), "장안", new HexCoord(1, 2), new FactionId(1), 3000, size);
        var footprint = CastleFootprint.TilesFor(city).ToHashSet();
        var site = new SiegeSite(city.Position, city.Owner, footprint.ToArray(), city.Id);
        var target = city.Position;
        for (var i = 0; i < 10; i++) target += direction;
        var passability = new PassabilityMap(new HexMap(-15, 18, -15, 18), [], [city]);
        var simulator = new MovementSimulator(passability);
        var scout = new FieldUnit(new UnitId(999), city.Owner, city.Position, 1, 0, 1,
            MovementDomain.Land, UnitMode.March, target, 999);
        var expectedExit = simulator.Advance([scout], 1, [site]).Units.Single().Position;
        var units = Enumerable.Range(1, count).Select(id =>
        {
            var field = new FieldUnit(new UnitId(id), city.Owner, city.Position, 2, 0, 1,
                MovementDomain.Land, UnitMode.March, target, id);
            return new CombatUnit(field, new CombatStats(1000, 1, 1), new TroopPool(1000, 0),
                UnitCombatState.Create(0), TroopCode: "swordsman");
        }).ToArray();
        var state = new GameState(1, 1, [], [city], [], FieldArmies: units);
        var engine = new CampaignEngine(new AdvanceOrchestrator(simulator,
            new CombatPhaseResolver(new BattleResolver(60), 70)), new WorldEngine(new BalanceConfig(100)));
        engine.AdvanceWeek(state, out var turns);
        var scene = new CampaignMapScene();
        try
        {
            typeof(CampaignMapScene).GetMethod("BuildAnimation", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(scene, [units.ToDictionary(u => u.Id.Value, _ => city.Position), turns,
                    Array.Empty<SiegeExchange>(), Array.Empty<CaptureReport>(), state]);
            var moves = Read<List<(double Time, int UnitId, HexCoord To)>>(scene, "_animSteps");
            var firstMoves = moves.GroupBy(m => m.UnitId).ToDictionary(g => g.Key, g => g.OrderBy(m => m.Time).First());
            if (firstMoves.Count != count || firstMoves.Any(pair => pair.Value.To != expectedExit))
                throw new InvalidOperationException($"{size}/{direction}/{count}: queue used another exit");
            var orderedTimes = firstMoves.OrderBy(pair => pair.Key).Select(pair => pair.Value.Time).ToArray();
            if (!orderedTimes.Zip(orderedTimes.Skip(1), (a, b) => a < b).All(value => value))
                throw new InvalidOperationException($"{size}/{direction}/{count}: queue order was not preserved");
        }
        finally { scene.Free(); }
    }

    private static void CheckPlaybackDeploymentVisibility()
    {
        var field = new FieldUnit(new UnitId(1), new FactionId(1), new HexCoord(0, 0),
            2, 0, 1, MovementDomain.Land, UnitMode.March, new HexCoord(4, 0), 0);
        var waiting = new CombatUnit(field, new CombatStats(1000, 1, 1), new TroopPool(1000, 0),
            UnitCombatState.Create(0), TroopCode: "swordsman", OriginCity: new CityId(1),
            EgressDirection: DeploymentDirection.East, EgressExit: new HexCoord(1, 0), AwaitingEgress: true);
        var scene = new CampaignMapScene();
        try
        {
            var released = Read<HashSet<int>>(scene, "_playbackReleasedDeployments");
            var prepare = typeof(CampaignMapScene).GetMethod("PlaybackDeploymentState",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            var before = (CombatUnit)prepare.Invoke(scene, [waiting])!;
            if (!before.IsWaitingDeployment)
                throw new InvalidOperationException("Pending deployment became visible before its release event");
            released.Add(waiting.Id.Value);
            var after = (CombatUnit)prepare.Invoke(scene, [waiting])!;
            if (after.IsWaitingDeployment || after.AwaitingEgress)
                throw new InvalidOperationException("Released deployment was hidden again by playback vision");
        }
        finally { scene.Free(); }
    }

    private static T Read<T>(object instance, string name) =>
        (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
}
