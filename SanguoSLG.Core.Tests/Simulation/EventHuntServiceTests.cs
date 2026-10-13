namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Simulation.RenewalMovement;
using SanguoSLG.Core.Spatial;
using Xunit;

public class EventHuntServiceTests
{
    private static readonly EventUnitDefinition Tiger = new EventUnitLoader()
        .LoadFromDirectory(TestData.DataDirectory()).Single();
    private static readonly City City = new(new(1), "발견성", new(0, 0), new(1), 1000);
    private static GameState World => new(1, 190, [], [City], [],
        ExplorationDiscoveries: [new(1, new(1), City.Id, new(1), ExplorationResultKind.DivineBeast,
            "divine_beast_trace")]);
    private static HexMap Map => new(-8, 8, -8, 8,
        new Dictionary<HexCoord, TerrainType> { [new(2, 0)] = TerrainType.Forest, [new(3, 0)] = TerrainType.Mountain });
    private static EventHuntService Service => new(Map, [Tiger]);

    private static GameState Spawn()
        => Service.SpawnDue(Service.ScheduleDiscoveries(World), 8, out _);

    [Fact]
    public void 단서는_한번만_예약하고_7일후_한마리만_출현한다()
    {
        var scheduled = Service.ScheduleDiscoveries(World);
        Assert.Single(Service.ScheduleDiscoveries(scheduled).Encounters);
        Assert.Empty(Service.SpawnDue(scheduled, 7, out var early).Armies);
        Assert.Empty(early);
        var spawned = Service.SpawnDue(scheduled, 8, out var events);
        Assert.Equal(new HexCoord(2, 0), Assert.Single(spawned.Armies).Field.Position);
        Assert.Equal(WorldEventKind.EventUnitAppeared, Assert.Single(events).Kind);
        Assert.Single(Service.SpawnDue(spawned, 99, out var repeated).Armies);
        Assert.Empty(repeated);
        var restored = SaveService.Deserialize(SaveService.Serialize(spawned));
        Assert.Single(Service.SpawnDue(restored, 99, out _).Armies);
    }

    [Fact]
    public void 시설과_겹치는_장소는_건너뛰고_장소가_없으면_예약을_유지한다()
    {
        var blocked = Service.ScheduleDiscoveries(World) with
        { FacilityPlacements = [new(City.Id, new(2, 0), "farm", 1000)] };
        Assert.Equal(new HexCoord(3, 0), Assert.Single(Service.SpawnDue(blocked, 8, out _).Armies).Field.Position);
        var noTerrain = new EventHuntService(new HexMap(-8, 8, -8, 8), [Tiger]);
        var waiting = noTerrain.SpawnDue(blocked, 8, out _);
        Assert.Empty(waiting.Armies);
        Assert.Null(Assert.Single(waiting.Encounters).Unit);
    }

    [Fact]
    public void 두도시가_단서를_찾아도_활동중인_대호는_한마리다()
    {
        var world = World with { ExplorationDiscoveries = World.Discoveries.Append(
            new(2, new(2), City.Id, new(2), ExplorationResultKind.DivineBeast, "divine_beast_trace")).ToList() };
        var spawned = Service.SpawnDue(Service.ScheduleDiscoveries(world), 9, out _);
        var next = Service.SpawnDue(spawned, 30, out _);
        Assert.Single(next.Armies);
        Assert.Single(next.Encounters, e => e.Unit is null);
    }

    [Fact]
    public void 대호는_약한육상부대를_추격하고_해상과_주둔부대는_무시한다()
    {
        var spawned = Spawn();
        var tiger = Assert.Single(spawned.Armies);
        CombatUnit Target(int id, int troops) => tiger with
        { Field = tiger.Field with { Id = new(id), Owner = new(1), Position = new(3, 0) },
            Pool = new(troops, 0), RenewalPosition = RenewalHexSpace.Center(new(3, 0)), TroopCode = "swordsman" };
        var strong = Target(2, 10000);
        var weak = Target(3, 5000);
        var naval = Target(4, 1) with { Field = Target(4, 1).Field with { Domain = MovementDomain.DeepWater } };
        var sheltered = Target(5, 1);
        var state = spawned with { FieldArmies = [tiger, strong, weak, naval, sheltered],
            FieldBuildings = [new(new(1), "fort", new(1), new(3, 0), 4000, 0, 0, GarrisonUnit: sheltered.Id)] };
        var planned = Service.PlanDaily(state).Armies.Single(u => u.Id == tiger.Id);
        Assert.Equal(weak.Id, planned.Field.AssignedUnitTarget);
        var idle = Service.PlanDaily(spawned).Armies.Single();
        Assert.Equal(UnitMode.Standby, idle.Field.Mode);
        Assert.Null(idle.Field.Target);
    }
}
