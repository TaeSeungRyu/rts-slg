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
        int speed = 2, int detection = 2, int attackRange = 1, int commandOrder = 0,
        int rangeCastle = 1) =>
        new(new UnitId(id), new FactionId(owner), pos, speed, detection, attackRange,
            MovementDomain.Land, mode, target, commandOrder, rangeCastle);

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

    // ── 케이스 3 — 정면 자동 교전 ──

    [Fact]
    public void 케이스3a_같은칸경합은_명령순번앞선유닛이차지하고_인접해서전투로들어간다()
    {
        // 짝수 거리(4칸)로 마주 보게 두면 가운데 (2,0)을 동시에 노리는 스텝이 생긴다.
        // 예전엔 둘 다 멈춰 한 칸 벌어진 채 헛교전이었으나, 이제 명령 순번(A1=1<E1=2)이
        // 앞선 A1이 그 칸을 차지하고 E1은 막힌다 → 인접(사거리 1) → 전투 페이즈
        var a1 = Unit(1, owner: 1, new HexCoord(0, 0), UnitMode.Attack, target: new HexCoord(4, 0),
            speed: 1, detection: 2, attackRange: 1);
        var e1 = Unit(2, owner: 2, new HexCoord(4, 0), UnitMode.Attack, target: new HexCoord(0, 0),
            speed: 1, detection: 2, attackRange: 1);

        var result = PlainField().Advance(new[] { a1, e1 });

        Assert.Equal(StopReason.EnemyInRange, result.Reason);

        var a1Final = result.Units.Single(u => u.Id.Value == 1).Position;
        var e1Final = result.Units.Single(u => u.Id.Value == 2).Position;

        // 우선순위 A1이 경합 칸(2,0)을 차지하고, 둘은 인접(사거리 1 안)에서 멈춘다
        Assert.Equal(new HexCoord(2, 0), a1Final);
        Assert.Equal(1, a1Final.Distance(e1Final));
        Assert.NotEqual(a1Final, e1Final);
    }

    [Fact]
    public void 케이스3b_인접한적끼리_자리를맞바꾸려하면_자동교전한다()
    {
        // 인접(1칸)에서 서로의 칸으로 이동 명령 = 정면 맞부딪힘
        var a1 = Unit(1, owner: 1, new HexCoord(0, 0), UnitMode.Attack, target: new HexCoord(1, 0),
            speed: 1, detection: 1, attackRange: 0);
        var e1 = Unit(2, owner: 2, new HexCoord(1, 0), UnitMode.Attack, target: new HexCoord(0, 0),
            speed: 1, detection: 1, attackRange: 0);

        var result = PlainField().Advance(new[] { a1, e1 });

        Assert.Equal(StopReason.Engaged, result.Reason);
        Assert.Contains(result.Ticks.SelectMany(t => t.Events), e => e.Kind == TickEventKind.Engaged);

        // 자리를 맞바꾸지 못하고 제자리를 지킨다
        Assert.Equal(new HexCoord(0, 0), result.Units.Single(u => u.Id.Value == 1).Position);
        Assert.Equal(new HexCoord(1, 0), result.Units.Single(u => u.Id.Value == 2).Position);
    }

    // ── 케이스 4 — 연쇄 이동: a→b, b→c는 충돌이 아니다 ──

    [Fact]
    public void 케이스4_적이비우는칸으로_추격유닛이연쇄이동한다()
    {
        // A1(0,0) 공격이 앞선 E1(1,0)을 쫓고, E1(행군)은 (2,0)으로 비켜 간다.
        // A1이 E1의 옛 칸(1,0)으로 들어가는 것은 충돌이 아니라 연쇄 이동 — 성립한다.
        var a1 = Unit(1, owner: 1, new HexCoord(0, 0), UnitMode.Attack, target: new HexCoord(9, 0),
            speed: 1, detection: 2, attackRange: 1);
        var e1 = Unit(2, owner: 2, new HexCoord(1, 0), UnitMode.March, target: new HexCoord(9, 0),
            speed: 1, detection: 2, attackRange: 1);

        var result = PlainField().Advance(new[] { a1, e1 });

        // 충돌(교전)이 아니라 이동이 성립했다
        Assert.DoesNotContain(result.Ticks.SelectMany(t => t.Events), e => e.Kind == TickEventKind.Engaged);

        var a1Final = result.Units.Single(u => u.Id.Value == 1).Position;
        var e1Final = result.Units.Single(u => u.Id.Value == 2).Position;

        // A1이 E1이 비운 (1,0)으로 실제 진입했고(연쇄), E1은 앞으로 나아갔다
        Assert.Equal(new HexCoord(1, 0), a1Final);
        Assert.Equal(new HexCoord(2, 0), e1Final);

        // 이동 후 사거리 안에 들어와 진행이 멈춘다(그 뒤 전투 페이즈: A1 공격·E1 반격 없음)
        Assert.Equal(StopReason.EnemyInRange, result.Reason);
    }

    // ── 케이스 5 — 다대일 조우: 두 부대가 하나를 협격 ──

    [Fact]
    public void 케이스5_두공격부대가_양쪽에서다가오면_둘다사거리안에서정지한다()
    {
        // A1(서), A2(동)이 가운데 정지한 E1을 향해 대칭으로 다가온다 → 같은 스텝에 양쪽 인접
        var a1 = Unit(1, owner: 1, new HexCoord(0, 2), UnitMode.Attack, target: new HexCoord(4, 2),
            speed: 1, detection: 2, attackRange: 1);
        var a2 = Unit(2, owner: 1, new HexCoord(8, 2), UnitMode.Attack, target: new HexCoord(4, 2),
            speed: 1, detection: 2, attackRange: 1);
        var e1 = Unit(3, owner: 2, new HexCoord(4, 2), UnitMode.March, target: null,
            speed: 1, detection: 2, attackRange: 1);

        var result = new MovementSimulator(new PassabilityMap(new HexMap(0, 8, 0, 4), [], []))
            .Advance(new[] { a1, a2, e1 });

        Assert.Equal(StopReason.EnemyInRange, result.Reason);

        var e1Final = result.Units.Single(u => u.Id.Value == 3).Position;
        var a1Final = result.Units.Single(u => u.Id.Value == 1).Position;
        var a2Final = result.Units.Single(u => u.Id.Value == 2).Position;

        // E1은 제자리, 두 공격 부대 모두 사거리(1) 안에서 멈춰 협격 대형이 된다
        Assert.Equal(new HexCoord(4, 2), e1Final);
        Assert.Equal(1, a1Final.Distance(e1Final));
        Assert.Equal(1, a2Final.Distance(e1Final));
    }

    // ── 케이스 6 — 추격 중단: 시야를 잃으면 원래 목표로 복귀 ──

    [Fact]
    public void 케이스6_추격하던적이_탐지범위를벗어나면_추격을버리고_원래목표로복귀한다()
    {
        // A1(공격, 북쪽 목표)이 빠른 척후 E1을 잠깐 탐지·추격하다, E1이 더 빨라
        // 탐지 밖으로 벗어나면 추격을 버리고 원래 목표(0,6)로 돌아가 도착한다.
        // E1 탐지 1 — A1 근처에서도 감속하지 않아 속도 3을 온전히 써 벗어난다.
        var a1 = Unit(1, owner: 1, new HexCoord(0, 0), UnitMode.Attack, target: new HexCoord(0, 6),
            speed: 2, detection: 2, attackRange: 1);
        var e1 = Unit(2, owner: 2, new HexCoord(2, 0), UnitMode.March, target: new HexCoord(12, 0),
            speed: 3, detection: 1, attackRange: 1);

        var result = new MovementSimulator(new PassabilityMap(new HexMap(0, 12, 0, 6), [], []))
            .Advance(new[] { a1, e1 });

        var events = result.Ticks.SelectMany(t => t.Events).ToList();
        Assert.Contains(events, e => e.Kind == TickEventKind.PursuitStarted && e.Unit.Value == 1);
        Assert.Contains(events, e => e.Kind == TickEventKind.PursuitEnded && e.Unit.Value == 1);

        // 추격을 버리고 원래 목표(0,6)로 복귀해 도착한다 — 목표가 보존됐다
        Assert.Equal(StopReason.AllArrived, result.Reason);
        Assert.Equal(new HexCoord(0, 6), result.Units.Single(u => u.Id.Value == 1).Position);
    }

    // ── 케이스 7 — 탐지 동률: 가장 가깝고, 같으면 명령 순번 ──

    [Fact]
    public void 케이스7_같은거리의두적은_명령순번앞선쪽을_추격한다()
    {
        // E1(id2)·E2(id3) 모두 A1에서 거리 2 — 동률이면 명령 순번(=id) 앞선 E1을 쫓는다
        var a1 = Unit(1, owner: 1, new HexCoord(3, 3), UnitMode.Attack, target: null,
            speed: 1, detection: 2, attackRange: 1);
        var e1 = Unit(2, owner: 2, new HexCoord(5, 3), UnitMode.March, target: null, speed: 1);
        var e2 = Unit(3, owner: 2, new HexCoord(3, 1), UnitMode.March, target: null, speed: 1);

        var result = new MovementSimulator(new PassabilityMap(new HexMap(0, 8, 0, 6), [], []))
            .Advance(new[] { a1, e1, e2 });

        var firstPursuit = result.Ticks
            .SelectMany(t => t.Events)
            .First(e => e.Kind == TickEventKind.PursuitStarted && e.Unit.Value == 1);
        Assert.Equal(2, firstPursuit.Other!.Value.Value); // E1(id2)
    }

    [Fact]
    public void 케이스7_더가까운적이있으면_명령순번과무관하게_가까운쪽을추격한다()
    {
        // E1(id2)은 거리 2, E2(id3)은 거리 1 — 명령 순번은 E1이 앞서도 더 가까운 E2를 쫓는다
        var a1 = Unit(1, owner: 1, new HexCoord(3, 3), UnitMode.Attack, target: null,
            speed: 1, detection: 2, attackRange: 1);
        var e1 = Unit(2, owner: 2, new HexCoord(5, 3), UnitMode.March, target: null, speed: 1);
        var e2 = Unit(3, owner: 2, new HexCoord(4, 3), UnitMode.March, target: null, speed: 1);

        var result = new MovementSimulator(new PassabilityMap(new HexMap(0, 8, 0, 6), [], []))
            .Advance(new[] { a1, e1, e2 });

        var firstPursuit = result.Ticks
            .SelectMany(t => t.Events)
            .First(e => e.Kind == TickEventKind.PursuitStarted && e.Unit.Value == 1);
        Assert.Equal(3, firstPursuit.Other!.Value.Value); // E2(id3), 더 가까움
    }

    // ── 케이스 8 — 아군에 막힘: 교전 없음 + 3일 정지 ──

    [Fact]
    public void 케이스8_아군이외길을막으면_우회없이_3일뒤정지한다()
    {
        // 한 줄짜리 외길. 아군 blocker가 (2,0)에 버티고, mover는 그 너머 (4,0)이 목표.
        // 경로는 1회 계산이라 우회하지 않고 blocker 앞에서 기다린다 → 3일 뒤 정지 알림.
        var map = new HexMap(0, 4, 0, 0);
        var sim = new MovementSimulator(new PassabilityMap(map, [], []));
        var blocker = Unit(1, owner: 1, new HexCoord(2, 0), UnitMode.March, target: null, speed: 1);
        var mover = Unit(3, owner: 1, new HexCoord(0, 0), UnitMode.March, target: new HexCoord(4, 0), speed: 1);

        var result = sim.Advance(new[] { blocker, mover });

        Assert.Equal(StopReason.Blocked, result.Reason);
        // 아군끼리는 교전하지 않는다
        Assert.DoesNotContain(result.Ticks.SelectMany(t => t.Events), e => e.Kind == TickEventKind.Engaged);
        // 한 칸 전진해 blocker 바로 뒤(1,0)에서 멈춘다 — 우회하지 않는다
        Assert.Equal(new HexCoord(1, 0), result.Units.Single(u => u.Id.Value == 3).Position);
    }

    // ── 케이스 9 — 아군에 막힌 추격 부대의 국소 우회 (2026-08-12) ──

    [Fact]
    public void 케이스9_추격경로가_아군에막히면_옆으로우회해_적에게붙는다()
    {
        // 추격 경로 첫 칸을 아군이 점유해도, 목표에 더 가까운 빈 이웃으로 국소 우회해 적에 인접한다.
        // 대군 전투에서 아군 뒤에 갇혀 영원히 대기하던 문제를 막는다. (지형만 보는 A*는 그대로)
        var map = new HexMap(0, 5, -1, 2);
        var sim = new MovementSimulator(new PassabilityMap(map, [], []));
        var chaser = Unit(1, owner: 1, new HexCoord(1, 1), UnitMode.Attack, target: new HexCoord(5, 1), detection: 2);
        var ally = Unit(2, owner: 1, new HexCoord(1, 0), UnitMode.March, target: null);   // 직행 경로를 막는다
        var enemy = Unit(3, owner: 2, new HexCoord(0, 0), UnitMode.March, target: null);

        var result = sim.Advance(new[] { chaser, ally, enemy });

        Assert.Equal(StopReason.EnemyInRange, result.Reason);
        var chaserFinal = result.Units.Single(u => u.Id.Value == 1).Position;
        Assert.Equal(1, chaserFinal.Distance(new HexCoord(0, 0))); // 우회해 적에 인접했다
        Assert.NotEqual(new HexCoord(1, 1), chaserFinal);          // 제자리에 갇히지 않았다
    }

    [Fact]
    public void 지형이동패널티_소하천에들어가면_그날_이동이_준다()
    {
        // (2,0)이 소하천. 속도2 부대가 (1,0)→소하천 진입 시 그 날 예산을 다 써 거기서 멈춘다(1칸).
        var terrain = new System.Collections.Generic.Dictionary<HexCoord, TerrainType>
        {
            [new HexCoord(2, 0)] = TerrainType.River,
        };
        var sim = new MovementSimulator(new PassabilityMap(new HexMap(0, 10, 0, 0, terrain), [], []));
        var u = Unit(1, owner: 1, new HexCoord(1, 0), UnitMode.March, target: new HexCoord(8, 0), speed: 2, detection: 2);

        var result = sim.Advance(new[] { u }, maxDays: 1);

        // 평지였다면 (3,0)까지 갔겠지만 소하천 진입으로 (2,0)에서 멈춘다.
        Assert.Equal(new HexCoord(2, 0), result.Units.Single(x => x.Id.Value == 1).Position);
    }

    [Fact]
    public void 케이스9b_직진이_아군에막히면_같은거리_옆칸으로_측면우회한다()
    {
        // (3,2) 아군이 직진 칸을 막고, 더 가까운 빈 칸이 없어도, 같은 거리의 옆칸(3,1)로 돌아간다.
        // 예전엔 제자리 대기했다(뒷열·정체 부대가 안 움직임).
        var map = new HexMap(0, 6, 0, 4);
        var sim = new MovementSimulator(new PassabilityMap(map, [], []));
        var mover = Unit(1, owner: 1, new HexCoord(2, 2), UnitMode.Attack, target: new HexCoord(5, 2), speed: 1, detection: 1);
        var ally = Unit(2, owner: 1, new HexCoord(3, 2), UnitMode.March, target: null, speed: 1); // 직진 칸을 막음

        var result = sim.Advance(new[] { mover, ally }, maxDays: 1);

        var moverFinal = result.Units.Single(u => u.Id.Value == 1).Position;
        Assert.NotEqual(new HexCoord(2, 2), moverFinal);      // 제자리에 갇히지 않았다
        Assert.Equal(new HexCoord(3, 1), moverFinal);          // 같은 거리 옆칸으로 측면 우회
    }

    // ── 성 접적 정지(design-movement) — 야전 접적과 같은 규칙이 성에도 적용된다 ──

    [Fact]
    public void 성접적_먼저도착한부대가_공성사거리에들면_그날로진행이끊기고_뒤처진부대는남는다()
    {
        // 기병(속도3)이 3일차에 성 인접(7,0) 도착 → 그 날로 진행 중단. 도검(속도2)은 아직 못 도착.
        var castle = new SiegeSite(new HexCoord(8, 0), new FactionId(2));
        var cav = Unit(1, owner: 1, new HexCoord(0, 0), UnitMode.Attack, target: new HexCoord(7, 0), speed: 3);
        var foot = Unit(2, owner: 1, new HexCoord(0, -1), UnitMode.Attack, target: new HexCoord(7, -1), speed: 2);

        var result = PlainField().Advance(new[] { cav, foot }, castles: new[] { castle });

        Assert.Equal(StopReason.CastleInRange, result.Reason);
        Assert.Equal(3, result.Days);
        Assert.Equal(new HexCoord(7, 0), result.Units.Single(u => u.Id.Value == 1).Position);
        Assert.NotEqual(new HexCoord(7, -1), result.Units.Single(u => u.Id.Value == 2).Position);
    }

    [Fact]
    public void 성접적_공성사거리2부대는_거리2에서_더다가가지않는다()
    {
        // 투석기(공성 사거리 2)는 성에서 거리 2 칸에 든 순간 홀드 — 반격 사거리 1 밖을 유지한다.
        var castle = new SiegeSite(new HexCoord(8, 0), new FactionId(2));
        var cat = Unit(1, owner: 1, new HexCoord(0, 0), UnitMode.Attack, target: new HexCoord(7, 0),
            speed: 2, rangeCastle: 2);

        var result = PlainField().Advance(new[] { cat }, castles: new[] { castle });

        Assert.Equal(StopReason.CastleInRange, result.Reason);
        Assert.Equal(2, result.Units.Single().Position.Distance(castle.Position));
    }

    [Fact]
    public void 성접적_행군모드는_성옆을지나가도_멈추지않는다()
    {
        var castle = new SiegeSite(new HexCoord(4, 1), new FactionId(2));
        var u = Unit(1, owner: 1, new HexCoord(0, 0), UnitMode.March, target: new HexCoord(8, 0), speed: 2);

        var result = PlainField().Advance(new[] { u }, castles: new[] { castle });

        Assert.Equal(StopReason.AllArrived, result.Reason);
        Assert.Equal(new HexCoord(8, 0), result.Units.Single().Position);
    }

    [Fact]
    public void 성접적_아군성은_정지시키지않는다()
    {
        var castle = new SiegeSite(new HexCoord(4, 1), new FactionId(1));
        var u = Unit(1, owner: 1, new HexCoord(0, 0), UnitMode.Attack, target: new HexCoord(8, 0), speed: 2);

        var result = PlainField().Advance(new[] { u }, castles: new[] { castle });

        Assert.Equal(StopReason.AllArrived, result.Reason);
        Assert.Equal(new HexCoord(8, 0), result.Units.Single().Position);
    }

    [Fact]
    public void 성입성_목표가자기성이면_마지막스텝으로_입성해_야전에서빠진다()
    {
        // 소속 성(8,0)으로 복귀 명령. 성 타일로 들어가는 마지막 스텝이 곧 입성 —
        // Units에서 빠지고 Entered에 보고된다.
        var castle = new SiegeSite(new HexCoord(8, 0), new FactionId(1));
        var u = Unit(1, owner: 1, new HexCoord(3, 0), UnitMode.March, target: new HexCoord(8, 0), speed: 2);

        var result = PlainField().Advance(new[] { u }, castles: new[] { castle });

        Assert.Equal(new[] { new UnitId(1) }, result.EnteredCastle);
        Assert.DoesNotContain(result.Units, x => x.Id.Value == 1);
        Assert.Contains(result.Ticks.SelectMany(t => t.Events),
            e => e.Kind == TickEventKind.EnteredCastle && e.Unit.Value == 1);
    }

    [Fact]
    public void 성입성_이동력이남으면_같은날_이동에이어_바로입성한다()
    {
        // 이동력 2: 1칸 이동(7,0) + 입성 스텝 = 2칸 소비, 그날 입성.
        var castle = new SiegeSite(new HexCoord(8, 0), new FactionId(1));
        var u = Unit(1, owner: 1, new HexCoord(6, 0), UnitMode.March, target: new HexCoord(8, 0), speed: 2);

        var result = PlainField().Advance(new[] { u }, maxDays: 1, castles: new[] { castle });

        Assert.Equal(new[] { new UnitId(1) }, result.EnteredCastle);
    }

    [Fact]
    public void 성입성_이동력을다써서_인접에도착하면_그날은_입성못하고_다음날_들어간다()
    {
        // 이동력 2를 (6,0)→(7,0) 이동에 다 쓰면 인접해도 그날은 입성 불가(입성도 이동이다).
        var castle = new SiegeSite(new HexCoord(8, 0), new FactionId(1));
        var u = Unit(1, owner: 1, new HexCoord(5, 0), UnitMode.March, target: new HexCoord(8, 0), speed: 2);

        var day1 = PlainField().Advance(new[] { u }, maxDays: 1, castles: new[] { castle });
        Assert.Empty(day1.EnteredCastle);
        Assert.Equal(new HexCoord(7, 0), day1.Units.Single().Position);

        var day2 = PlainField().Advance(new[] { u }, maxDays: 2, castles: new[] { castle });
        Assert.Equal(new[] { new UnitId(1) }, day2.EnteredCastle);
    }

    [Fact]
    public void 성입성_적성이_목표라도_입성하지않는다()
    {
        // 적 성이 목표면 입성이 아니라 공성 접적 정지로 흘러간다.
        var castle = new SiegeSite(new HexCoord(8, 0), new FactionId(2));
        var u = Unit(1, owner: 1, new HexCoord(3, 0), UnitMode.Attack, target: new HexCoord(8, 0), speed: 2);

        var result = PlainField().Advance(new[] { u }, castles: new[] { castle });

        Assert.Empty(result.EnteredCastle);
        Assert.Equal(StopReason.CastleInRange, result.Reason);
        Assert.Contains(result.Units, x => x.Id.Value == 1);
    }

    // ── 수비대 출격 — 성 타일에서 나오는 첫 스텝부터가 이동(입성의 거울) ──

    [Fact]
    public void 성출격_성타일에서_출발한_유닛은_첫스텝부터_정상이동한다()
    {
        // 성 타일(2,0)은 통행 불가지만 그 위에서 출발하는 출격 부대는 정상적으로 걸어 나온다.
        var city = new City(new CityId(9), "성", new HexCoord(2, 0), new FactionId(1), 0);
        var sim = new MovementSimulator(new PassabilityMap(new HexMap(0, 10, -2, 2), [], [city]));
        var u = Unit(1, owner: 1, new HexCoord(2, 0), UnitMode.March, target: new HexCoord(5, 0), speed: 2);

        var result = sim.Advance(new[] { u }, maxDays: 1);

        Assert.Equal(new HexCoord(4, 0), result.Units.Single().Position); // 이동력 2 = 2칸
    }

    [Fact]
    public void 성출격_수비대_둘이_성타일에_겹쳐있어도_같은날_성밖으로_빠져나온다()
    {
        // 출격 대기 수비대는 성 타일에 겹쳐 설 수 있다. 같은 날 둘 다 나오되 겹치지 않는다.
        var city = new City(new CityId(9), "성", new HexCoord(2, 0), new FactionId(1), 0);
        var sim = new MovementSimulator(new PassabilityMap(new HexMap(0, 10, -2, 2), [], [city]));
        var a = Unit(1, owner: 1, new HexCoord(2, 0), UnitMode.March, target: new HexCoord(5, 0), speed: 2);
        var b = Unit(2, owner: 1, new HexCoord(2, 0), UnitMode.March, target: new HexCoord(5, 0), speed: 2);

        var result = sim.Advance(new[] { a, b }, maxDays: 1);

        var pa = result.Units.Single(x => x.Id.Value == 1).Position;
        var pb = result.Units.Single(x => x.Id.Value == 2).Position;
        Assert.NotEqual(pa, pb);
        Assert.NotEqual(new HexCoord(2, 0), pa);
        Assert.NotEqual(new HexCoord(2, 0), pb);
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
