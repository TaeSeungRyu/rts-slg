using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

public partial class PortRegressionQa : Node
{
    public override void _Ready()
    {
        try
        {
            var cities = (IReadOnlyList<City>)typeof(CampaignMapScene)
                .GetField("_cities", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            var map = CampaignMapScene.TestMap;
            var passability = new PassabilityMap(map, [], cities);
            var cases = 0;
            foreach (var source in cities.Where(c => c.Owner.Value == 1 && !c.IsPort))
            foreach (var port in cities.Where(c => c.Owner.Value == 1 && c.IsPort))
            {
                var army = new CombatUnit(new FieldUnit(new UnitId(1), source.Owner, source.Position,
                    2, 1, 0, MovementDomain.Land, UnitMode.March, port.Position, 1, ReturnCity: port.Id),
                    new CombatStats(1000, 1, 1), new TroopPool(1000, 0), UnitCombatState.Create(50),
                    TroopCode: "transport", IsTransport: true,
                    SupplyCargo: [new SupplyComponent("swordsman", 1000, 50)], CargoGold: 100);
                var engine = new CampaignEngine(new AdvanceOrchestrator(new MovementSimulator(passability),
                    new CombatPhaseResolver(new BattleResolver(60), 70)), new WorldEngine(new BalanceConfig(0)));
                var state = new GameState(1, 1, [], cities, [], FieldArmies: [army]);
                for (var week = 0; week < 3 && state.Armies.Count > 0; week++)
                    state = engine.AdvanceWeek(state, out _);
                if (state.Armies.Count != 0 || !state.Garrisons.Any(g => g.City == port.Id && g.Troops == 1000))
                    throw new InvalidOperationException($"입항 실패: {source.Name} → {port.Name}");
                cases++;
            }
            var navalCases = 0;
            var navalPathfinder = new HexPathfinder(h => passability.CanEnter(MovementDomain.DeepWater, h)
                || cities.Any(c => c.IsPort && CastleFootprint.TilesFor(c).Contains(h)));
            var waterTiles = map.Tiles()
                .Where(h => map.TerrainAt(h) is TerrainType.WaterShallow or TerrainType.WaterDeep)
                .ToList();
            foreach (var port in cities.Where(c => c.IsPort))
            {
                var target = waterTiles
                    .Where(h => h.Distance(port.Position) >= 3)
                    .OrderByDescending(h => h.Distance(port.Position))
                    .FirstOrDefault();
                if (target == default)
                    throw new InvalidOperationException($"출항 테스트용 바다 타일 부족: {port.Name}");
                var path = navalPathfinder.FindPath(port.Position, target);
                if (path.Count < 2)
                    throw new InvalidOperationException($"출항 경로 없음: {port.Name} → ({target.Q},{target.R})");
                navalCases++;
            }
            var view = new MapView3D();
            AddChild(view);
            view.Build(map, new HashSet<HexCoord>(), new TileConditionMap(), CampaignMapScene.TestPortTiles);
            if (view.FindChildren("*pier*", "", true, false).Count != 0)
                throw new InvalidOperationException("지형 항구 모델 중복 생성");
            var scene = new CampaignMapScene();
            typeof(CampaignMapScene).GetField("_view", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(scene, view);
            typeof(CampaignMapScene).GetField("_passability", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(scene, passability);
            foreach (var port in cities.Where(c => c.IsPort))
            {
                var yaw = (float)typeof(CampaignMapScene).GetMethod("PortFacingYaw", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(scene, [port])!;
                var path = port.Port == PortSize.Small ? "port-small" : "port-medium";
                var model = GD.Load<PackedScene>($"res://assets/models/{path}.glb").Instantiate<Node3D>();
                var piers = model.FindChildren("*plank*", "MeshInstance3D", true, false)
                    .OfType<Node3D>().Where(n => n.Name.ToString().StartsWith("pier")).ToArray();
                if (piers.Length == 0) throw new InvalidOperationException("잔교 메시 누락");
                var local = piers.Aggregate(Vector3.Zero, (sum, n) => sum + n.Position) / piers.Length;
                var direction = new Basis(Vector3.Up, yaw) * new Vector3(local.X, 0, local.Z).Normalized();
                var center = (Vector3)typeof(CampaignMapScene).GetMethod("CastleVisualCenter", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(scene, [port])!;
                var footprint = CastleFootprint.TilesFor(port).ToHashSet();
                var waterDirections = footprint.SelectMany(h => h.Neighbors()).Where(h => !footprint.Contains(h)
                    && map.Contains(h) && map.TerrainAt(h) is TerrainType.WaterShallow or TerrainType.WaterDeep or TerrainType.WaterRocks)
                    .Select(h => (view.HexToWorld(h) - center).Normalized());
                if (!waterDirections.Any(d => direction.Dot(d) > 0.95f))
                    throw new InvalidOperationException($"잔교가 물을 향하지 않음: {port.Name}");
                model.Free();
            }
            scene.Free();
            GD.Print($"PORT_QA PASS: {cases} actual-map transport routes; {navalCases} naval routes; 3 port mesh orientations; no duplicate piers");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
