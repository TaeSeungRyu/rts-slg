namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

/// <summary>
/// 이동 시뮬레이션 검증(doc/test/movement-cases.md). 케이스 번호를 메서드명에 붙인다.
/// </summary>
public class MovementSimulatorTests
{
    private static MovementSimulator PlainField()
    {
        // 지물·성 없는 평지 — 통행 제약은 맵 경계뿐
        var map = new HexMap(0, 20, -5, 5);
        return new MovementSimulator(new PassabilityMap(map, [], []));
    }

    private static FieldUnit Unit(
        int id, int owner, HexCoord pos, UnitMode mode, HexCoord? target,
        int speed = 2, int detection = 2, int attackRange = 1, int commandOrder = 0) =>
        new(new UnitId(id), new FactionId(owner), pos, speed, detection, attackRange,
            MovementDomain.Land, mode, target, commandOrder);

    // ── 케이스 1 — 공격모드 조우: 탐지 → 추격 → 사거리 정지 ──

    [Fact]
    public void 케이스1_공격모드가_먼목표로가다_적을탐지하면_추격후사거리에서멈춘다()
    {
        var a1 = Unit(1, owner: 1, new HexCoord(0, 0), UnitMode.Attack, target: new HexCoord(10, 0));
        var e1 = Unit(2, owner: 2, new HexCoord(8, 0), UnitMode.March, target: null);

        var result = PlainField().Advance(new[] { a1, e1 });

        // 사거리 안의 적을 만나 진행이 멈춘다
        Assert.Equal(StopReason.EnemyInRange, result.Reason);

        var a1Final = result.Units.Single(u => u.Id.Value == 1);
        var e1Final = result.Units.Single(u => u.Id.Value == 2);

        // 적은 제자리, A1은 인접(사거리 1)에서 멈춘다 — 겹치거나 지나치지 않는다
        Assert.Equal(new HexCoord(8, 0), e1Final.Position);
        Assert.Equal(1, a1Final.Position.Distance(e1Final.Position));
    }

    [Fact]
    public void 케이스1_탐지범위밖에서는_원래목표를향하다_탐지순간_추격으로전환한다()
    {
        var a1 = Unit(1, owner: 1, new HexCoord(0, 0), UnitMode.Attack, target: new HexCoord(10, 0));
        var e1 = Unit(2, owner: 2, new HexCoord(8, 0), UnitMode.March, target: null);

        var result = PlainField().Advance(new[] { a1, e1 });

        // 추격 시작 사건이 정확히 한 번 나오고, 그 대상은 E1이다
        var pursuit = result.Ticks
            .SelectMany(t => t.Events)
            .Where(e => e.Kind == TickEventKind.PursuitStarted)
            .ToList();
        Assert.Single(pursuit);
        Assert.Equal(1, pursuit[0].Unit.Value);
        Assert.Equal(2, pursuit[0].Other!.Value.Value);
    }

    [Fact]
    public void 케이스1_추격전환은_적이탐지범위2안에들어온뒤에일어난다()
    {
        var a1 = Unit(1, owner: 1, new HexCoord(0, 0), UnitMode.Attack, target: new HexCoord(10, 0));
        var e1 = Unit(2, owner: 2, new HexCoord(8, 0), UnitMode.March, target: null);

        var result = PlainField().Advance(new[] { a1, e1 });

        // 추격이 시작된 틱에서 A1은 E1의 탐지 범위(2) 안에 있어야 한다
        var pursuitTick = result.Ticks.First(t =>
            t.Events.Any(e => e.Kind == TickEventKind.PursuitStarted));
        var a1AtPursuit = pursuitTick.Units.Single(u => u.Id.Value == 1);
        Assert.True(a1AtPursuit.Position.Distance(new HexCoord(8, 0)) <= 2);
    }

    [Fact]
    public void 케이스1_마지막틱에_사거리정지사건이_기록된다()
    {
        var a1 = Unit(1, owner: 1, new HexCoord(0, 0), UnitMode.Attack, target: new HexCoord(10, 0));
        var e1 = Unit(2, owner: 2, new HexCoord(8, 0), UnitMode.March, target: null);

        var result = PlainField().Advance(new[] { a1, e1 });

        var last = result.Ticks[^1];
        Assert.Contains(last.Events, e => e.Kind == TickEventKind.Halted && e.Unit.Value == 1);
    }

    // ── 케이스 2 — 행군모드 통과: 무시 + 감속 ──

    [Fact]
    public void 케이스2_행군모드는_적을무시하고_목표까지지나간다()
    {
        var a1 = Unit(1, owner: 1, new HexCoord(0, 2), UnitMode.March, target: new HexCoord(16, 2),
            speed: 3, detection: 3);
        var e1 = Unit(2, owner: 2, new HexCoord(8, 3), UnitMode.March, target: null,
            speed: 2, detection: 2, attackRange: 2);

        var result = PlainField().Advance(new[] { a1, e1 });

        // 무시하고 계속 가 목표에 도착한다 — 추격도 정지도 없다
        Assert.Equal(StopReason.AllArrived, result.Reason);
        Assert.DoesNotContain(result.Ticks.SelectMany(t => t.Events),
            e => e.Kind == TickEventKind.PursuitStarted);
        Assert.Equal(new HexCoord(16, 2), result.Units.Single(u => u.Id.Value == 1).Position);
    }

    [Fact]
    public void 케이스2_적탐지범위안에서는_그날속도가_3에서2로준다()
    {
        var a1 = Unit(1, owner: 1, new HexCoord(0, 2), UnitMode.March, target: new HexCoord(16, 2),
            speed: 3, detection: 3);
        var e1 = Unit(2, owner: 2, new HexCoord(8, 3), UnitMode.March, target: null,
            speed: 2, detection: 2, attackRange: 2);

        var result = PlainField().Advance(new[] { a1, e1 });

        // 하루에 A1이 움직인 칸수를 센다
        var perDay = new Dictionary<int, int>();
        var last = new HexCoord(0, 2);
        foreach (var tick in result.Ticks)
        {
            var pos = tick.Units.Single(u => u.Id.Value == 1).Position;
            if (pos != last)
            {
                perDay[tick.Day] = perDay.GetValueOrDefault(tick.Day) + 1;
                last = pos;
            }
        }

        Assert.Contains(3, perDay.Values); // 평상시엔 속도 3
        Assert.Contains(2, perDay.Values); // 탐지 범위 안에서는 2로 감속
        Assert.True(perDay.Values.Max() <= 3); // 속도를 넘겨 가지 않는다
    }

    [Fact]
    public void 목표없는유닛끼리는_아무일도없이_전원도착으로끝난다()
    {
        var a1 = Unit(1, owner: 1, new HexCoord(0, 0), UnitMode.Attack, target: null);
        var e1 = Unit(2, owner: 2, new HexCoord(8, 0), UnitMode.March, target: null);

        var result = PlainField().Advance(new[] { a1, e1 });

        Assert.Equal(StopReason.AllArrived, result.Reason);
    }
}
