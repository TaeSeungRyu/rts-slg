namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public class FieldStratagemServiceTests
{
    private static readonly IReadOnlyList<Stratagem> Stratagems =
        new StratagemLoader().LoadFromDirectory(TestData.DataDirectory());

    private static FieldStratagemService Service(Func<HexCoord, TerrainType>? terrainAt = null)
        => new(Stratagems, terrainAt ?? (_ => TerrainType.Plains));

    private static CombatUnit Army(
        int id,
        int owner,
        HexCoord position,
        int intellect = 60,
        int masteryPoints = 0,
        int resource = -1,
        bool vanguard = true)
    {
        var field = new FieldUnit(
            new UnitId(id), new FactionId(owner), position, 2, 2, 1,
            MovementDomain.Land, UnitMode.March, null, id);
        var state = UnitCombatState.Create(intellect, masteryPoints: masteryPoints);
        if (resource >= 0)
        {
            state = state with { Resource = new StratagemResource(intellect, resource) };
        }

        return new CombatUnit(
            field,
            new CombatStats(10000, 10, 10),
            new TroopPool(10000, 0),
            state,
            Intellect: intellect,
            MaxTroops: 10000,
            VanguardId: vanguard ? new GeneralId(id) : null);
    }

    private static GameState World(int day, params CombatUnit[] armies)
        => new(day, 1, [], [], [], FieldArmies: armies);

    [Fact]
    public void 시전가능목록은_모략력과숙달을_모두만족한계략만_돌려준다()
    {
        var caster = Army(1, 1, new HexCoord(0, 0), intellect: 60, masteryPoints: 10, resource: 14);

        var castable = Service().Castable(World(1, caster), caster.Id);

        Assert.Equal(["douse", "cleanse", "smokescreen"], castable.Select(s => s.Code).ToArray());
    }

    [Fact]
    public void 선봉이없거나_이미계략준비중이면_시전가능목록이비어있다()
    {
        var noVanguard = Army(1, 1, new HexCoord(0, 0), vanguard: false);
        var reserved = Army(2, 1, new HexCoord(0, 1)) with
        {
            State = Army(2, 1, new HexCoord(0, 1)).State
                .ReserveStratagem(Stratagems.Single(s => s.Code == "douse"), new UnitId(2)),
        };

        Assert.Empty(Service().Castable(World(1, noVanguard), noVanguard.Id));
        Assert.Empty(Service().Castable(World(1, reserved), reserved.Id));
    }

    [Fact]
    public void 공격계략은_사거리와지형조건안의적만_대상으로돌려준다()
    {
        var caster = Army(1, 1, new HexCoord(0, 0), masteryPoints: 10);
        var riverEnemy = Army(2, 2, new HexCoord(1, 0));
        var plainsEnemy = Army(3, 2, new HexCoord(0, 1));
        var ally = Army(4, 1, new HexCoord(1, -1));
        var farEnemy = Army(5, 2, new HexCoord(3, 0));
        var service = Service(p => p == riverEnemy.Field.Position ? TerrainType.River : TerrainType.Plains);

        var targets = service.Targets(World(1, caster, riverEnemy, plainsEnemy, ally, farEnemy), caster.Id, "fire_plot");

        Assert.Equal([3], targets.Select(u => u.Id.Value).ToArray());
    }

    [Fact]
    public void 정화계략은_사거리안의아군을_대상으로돌려준다()
    {
        var caster = Army(1, 1, new HexCoord(0, 0));
        var ally = Army(2, 1, new HexCoord(2, 0));
        var enemy = Army(3, 2, new HexCoord(1, 0));

        var targets = Service().Targets(World(1, caster, ally, enemy), caster.Id, "douse");

        Assert.Equal([1, 2], targets.Select(u => u.Id.Value).ToArray());
    }

    [Fact]
    public void 예약하면_새상태와발동일비용지력차예상강도를_돌려준다()
    {
        var caster = Army(1, 1, new HexCoord(0, 0), intellect: 90, masteryPoints: 10);
        var target = Army(2, 2, new HexCoord(1, 0), intellect: 60);

        var result = Service().Reserve(World(15, caster, target), caster.Id, "fire_plot", target.Id);

        Assert.True(result.Ok, result.Error);
        Assert.Null(caster.State.Reservation);
        var reservation = result.State.Armies.Single(u => u.Id == caster.Id).State.Reservation;
        Assert.Equal("fire_plot", reservation!.Stratagem.Code);
        Assert.Equal(target.Id, reservation.TargetId);
        Assert.Equal(2, reservation.DaysUntilFire);
        Assert.Equal(17, result.Preview!.FireDay);
        Assert.Equal(15, result.Preview.Stratagem.Cost);
        Assert.Equal(30, result.Preview.IntellectDifference);
        Assert.Equal(130, result.Preview.StrengthPercent);
        Assert.Equal(90, result.State.Armies.Single(u => u.Id == caster.Id).State.Resource.Current);
    }

    [Theory]
    [InlineData(9, 60, "계략 숙달")]
    [InlineData(285, 44, "모략력")]
    public void 숙달이나모략력이부족하면_예약을거부한다(int masteryPoints, int resource, string error)
    {
        var caster = Army(1, 1, new HexCoord(0, 0), intellect: 60, masteryPoints, resource);
        var target = Army(2, 2, new HexCoord(1, 0));

        var state = World(1, caster, target);
        var result = Service().Reserve(state, caster.Id, "lightning", target.Id);

        Assert.False(result.Ok);
        Assert.Contains(error, result.Error);
        Assert.Same(state, result.State);
        Assert.Null(result.State.Armies.Single(u => u.Id == caster.Id).State.Reservation);
    }

    [Fact]
    public void 예약한계략은_캠페인진행에서_기존발동경로로효과를낸다()
    {
        var caster = Army(1, 1, new HexCoord(0, 0), intellect: 100, masteryPoints: 10);
        var target = Army(2, 2, new HexCoord(1, 0), intellect: 60);
        var reserved = Service().Reserve(World(1, caster, target), caster.Id, "fire_plot", target.Id);
        Assert.True(reserved.Ok, reserved.Error);

        var movement = new MovementSimulator(new PassabilityMap(new HexMap(-5, 5, -5, 5), [], []));
        var field = new AdvanceOrchestrator(
            movement,
            new CombatPhaseResolver(new BattleResolver(60), 70),
            terrainAt: _ => TerrainType.Plains);
        var campaign = new CampaignEngine(field, new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 100)));

        var after = campaign.AdvanceWeek(reserved.State, out var turns);

        var fired = turns.SelectMany(t => t.FiredStratagems).Single();
        Assert.Equal(caster.Id, fired.Key);
        Assert.Equal("fire_plot", fired.Value.Code);
        Assert.Null(after.Armies.Single(u => u.Id == caster.Id).State.Reservation);
        Assert.Equal(85, after.Armies.Single(u => u.Id == caster.Id).State.Resource.Current);
        Assert.Contains(after.Armies.Single(u => u.Id == target.Id).State.Statuses, s => s.Kind == StatusKind.Burn);
    }
}
