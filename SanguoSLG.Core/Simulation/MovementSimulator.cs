namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

/// <summary>
/// 일(日) 단위 동시 진행 시뮬레이터(doc/design-movement.md). "진행" 한 번은 최대 7일,
/// 하루는 유닛마다 자기 속도만큼의 칸 스텝으로 쪼갠다. 스텝마다 전 유닛의 다음 칸을
/// 동시에 확정하고, 탐지·추격·사거리 정지·자동 교전을 판정한다.
///
/// 결정론(CLAUDE.md 규칙 4): 유닛은 항상 UnitId 오름차순으로 처리하고, 경로는
/// 지형 통행(PassabilityMap)만으로 계산한다 — 유닛 점유는 스텝 해석에서만 다룬다.
/// </summary>
public sealed class MovementSimulator
{
    private readonly PassabilityMap _passability;

    public MovementSimulator(PassabilityMap passability) => _passability = passability;

    /// <summary>해당 통행 영역의 유닛이 이 칸에 들어갈 수 있는가(교란 후퇴 등 상위 계층용).</summary>
    public bool CanEnter(MovementDomain domain, HexCoord coord) => _passability.CanEnter(domain, coord);

    private sealed class Working
    {
        public FieldUnit Unit;
        public int MovedToday;
        public bool Pursuing;
        public int BlockedDays;
        public HexCoord? LastPos; // 직전에 있던 칸 — 측면 우회가 되돌아가 진동하는 것을 막는다

        // 남은 경로(현재 칸 제외). 비추격은 목표 지정 시 1회, 추격은 매일 재계산한다
        // (design-movement.md 규칙 4). null이면 미계산. 유닛에 막히면 재계산 없이 기다린다.
        public Queue<HexCoord>? Path;
        public int PathDay;

        // 경유지 계획: 지나갈 중간 지점들 + 최종 목표(마지막). 한 구간씩 순서대로 밟는다.
        // 경유지 없으면 [Target] 하나 — 기존 단일 목표 동작과 동일하다.
        public readonly List<HexCoord> Goals;
        public int GoalIdx;

        // 현재 향하는 구간 목표(전부 밟았으면 null).
        public HexCoord? CurrentGoal => GoalIdx < Goals.Count ? Goals[GoalIdx] : null;

        public Working(FieldUnit unit)
        {
            Unit = unit;
            Goals = BuildGoals(unit);
            while (GoalIdx < Goals.Count - 1 && Goals[GoalIdx] == unit.Position)
            {
                GoalIdx++; // 시작 칸과 같은 선두 경유지는 건너뛴다
            }
        }
    }

    // 경유지 목록(중간 지점) 뒤에 최종 목표를 붙여 구간 목표 순서를 만든다(연속 중복 제거).
    private static List<HexCoord> BuildGoals(FieldUnit unit)
    {
        var goals = new List<HexCoord>();
        if (unit.Waypoints is { } wps)
        {
            foreach (var wp in wps)
            {
                if (goals.Count == 0 ? wp != unit.Position : wp != goals[^1])
                {
                    goals.Add(wp);
                }
            }
        }

        if (unit.Target is { } t && (goals.Count == 0 ? t != unit.Position : t != goals[^1]))
        {
            goals.Add(t);
        }

        return goals;
    }

    /// <summary>한 번의 "진행"을 끝까지 계산한다(최대 <paramref name="maxDays"/>일).</summary>
    public AdvanceResult Advance(IReadOnlyList<FieldUnit> units, int maxDays = 7,
        IReadOnlyList<SiegeSite>? castles = null)
    {
        var work = units.OrderBy(u => u.Id.Value).Select(u => new Working(u)).ToList();
        var ticks = new List<MovementTick>();
        var entered = new List<UnitId>();
        var reason = StopReason.MaxDays;
        var daysElapsed = 0;

        for (var day = 1; day <= maxDays; day++)
        {
            daysElapsed = day;
            foreach (var w in work)
            {
                w.MovedToday = 0;
            }

            var movedThisDay = new HashSet<int>();

            while (true)
            {
                var events = new List<TickEvent>();

                // 공격모드 유닛의 추격 상태를 갱신한다(탐지 시작/시야 상실).
                // 도착 판정보다 먼저 — 목표 없이 서 있어도 적이 탐지에 들면 추격해야 한다.
                foreach (var w in work.Where(w => w.Unit.Mode == UnitMode.Attack))
                {
                    var seen = NearestEnemyWithin(w, work, w.Unit.Detection);
                    if (seen is not null && !w.Pursuing)
                    {
                        w.Pursuing = true;
                        w.Path = null; // 추격 시작 — 적으로 경로를 다시 잡는다
                        events.Add(new TickEvent(TickEventKind.PursuitStarted, w.Unit.Id, seen.Unit.Id));
                    }
                    else if (seen is null && w.Pursuing)
                    {
                        w.Pursuing = false;
                        w.Path = null; // 시야 상실 — 원래 목표로 경로를 다시 잡는다
                        events.Add(new TickEvent(TickEventKind.PursuitEnded, w.Unit.Id, null));
                    }
                }

                // 진행 중단 1 — 아무도 더 갈 곳이 없다(전원 목표 도착, 추격 중이면 도착 아님).
                // 성 타일 위 유닛은 목표가 없어도 "도착"이 아니다 — 성은 머무를 수 없어 반드시 내려선다.
                // 아직 아무 일도 없었으면(전원 도착 상태로 시작) 남은 일수를 한 번에 소진해, 진행 한 번이
                // 하루짜리 진행 여러 번으로 쪼개지지 않게 한다(사기·상태 tick의 주당 중복 적용 방지).
                if (work.All(w => NoIntent(w) && !OnCastle(w, castles)))
                {
                    return Finish(ticks, work, StopReason.AllArrived, ticks.Count == 0 ? maxDays : daysElapsed, entered);
                }

                // 이번 스텝에 움직이려는 유닛의 희망 칸을 모은다.
                // 경로는 캐시를 따른다 — 비추격은 목표까지 1회, 추격은 매일 재계산.
                var occupied = new HashSet<HexCoord>(work.Select(o => o.Unit.Position));
                // 성 타일에는 출격 대기 수비대가 겹쳐 서 있을 수 있다 — id 순서 첫 유닛 기준(결정론).
                var occupantOwner = new Dictionary<HexCoord, int>();
                foreach (var o in work)
                {
                    occupantOwner.TryAdd(o.Unit.Position, o.Unit.Owner.Value);
                }
                var desired = new Dictionary<int, HexCoord>();
                var enteringNow = new List<Working>();
                // 이번 스텝에 출격 부대가 이미 찜한 성 앞 칸(부대들이 겹치지 않고 6방향으로 흩어져
                // 나오도록). id 오름차순(먼저 편성 우선)으로 좋은 칸부터 가져간다.
                var claimed = new HashSet<HexCoord>();
                // 나가려 했으나 자리가 없어 못 나온 성 위 부대 — 대기 비용(#추가) 부과 대상.
                var egressBlocked = new List<int>();
                foreach (var w in work)
                {
                    if (w.MovedToday >= EffectiveSpeed(w, work))
                    {
                        continue;
                    }

                    // 성 타일 위(출격 대기)는 예외 — 성은 이동 불가 지형이라 머무를 수 없으니,
                    // 사거리 정지를 무시하고 반드시 내려서는 게이트 스텝을 먼저 밟는다(아래).
                    var onCastle = OnCastle(w, castles);

                    // 사거리 안에 적이 있으면 더 다가가지 않고 멈춰 싸운다(궁병 등은 사거리를 유지).
                    // 정지는 이 날 이동을 다 끝낸 뒤 판정한다 — 다른 부대는 계속 이동/재시도한다.
                    if (!onCastle && w.Unit.Mode == UnitMode.Attack && NearestEnemyWithin(w, work, w.Unit.AttackRange) is not null)
                    {
                        continue;
                    }

                    // 적 성이 공성 사거리 안이면 더 다가가지 않는다(투석기 등은 사거리를 유지).
                    if (!onCastle && w.Unit.Mode == UnitMode.Attack && CastleWithin(w, castles) is not null)
                    {
                        continue;
                    }

                    HexCoord? goalTile = null;
                    if (w.Pursuing)
                    {
                        goalTile = NearestEnemyWithin(w, work, w.Unit.Detection)?.Unit.Position;
                        if (goalTile is { } g && (w.Path is null || w.PathDay != day))
                        {
                            w.Path = BuildPath(w, g);
                            w.PathDay = day;
                        }
                    }
                    else if (w.CurrentGoal is { } t && t != w.Unit.Position)
                    {
                        goalTile = t;
                        if (w.Path is null)
                        {
                            w.Path = BuildPath(w, t);
                            w.PathDay = day;
                        }
                    }

                    // 출격 게이트 스텝: 성 타일 위 유닛은 빈·통행 이웃으로 내려선다. 목표가 있으면 그
                    // 방향(목표에 더 가까운 이웃)으로, 여러 부대는 서로 다른 이웃(claimed)으로 흩어져 나와
                    // 한 칸에 몰리지 않는다 — 6방향 중 목표 쪽 칸들을 먼저 편성 순서대로 나눠 갖는다.
                    // 목표가 없으면 빈 이웃에 하나씩 흩어져 성 앞에 대기한다. 적 점유 칸으로는 가지 않아
                    // 성문 위 교전을 열지 않고, 나갈 칸이 없으면(포위·혼잡) 이날은 성에서 대기한다.
                    if (onCastle)
                    {
                        var exit = goalTile is { } sg
                            ? GateStep(w, sg, occupied, claimed)
                            : GateStepAny(w, occupied, claimed);
                        if (exit is { } e)
                        {
                            w.Path = null; // 내려선 위치에서 경로를 다시 잡는다
                            desired[w.Unit.Id.Value] = e;
                            claimed.Add(e);
                        }
                        else
                        {
                            egressBlocked.Add(w.Unit.Id.Value);
                        }

                        continue;
                    }

                    if (w.Path is { Count: > 0 })
                    {
                        // 입성 = 이동의 마지막 한 스텝. 다음 칸이 자기 성이면 이동 예산을 쓰고
                        // 바로 성으로 들어간다(위 예산 체크를 통과한 유닛만 — 이동력 0이면 그날 불가).
                        if (IsOwnCastle(w, w.Path.Peek(), castles))
                        {
                            enteringNow.Add(w);
                            continue;
                        }

                        // 남의 성 타일로는 못 들어간다 — 경로는 목표 칸을 허용하지만(입성용),
                        // 목표 성이 적 소유(함락 등)면 그 앞에서 대기한다.
                        if (castles is not null && castles.Any(cs => cs.Position == w.Path.Peek()))
                        {
                            continue;
                        }

                        var step = StepOrDetour(w, w.Path.Peek(), goalTile, occupied, occupantOwner);
                        if (step != w.Path.Peek())
                        {
                            w.Path = null; // 우회했으니 새 위치에서 경로를 다시 잡는다(스텝은 항상 인접)
                        }

                        desired[w.Unit.Id.Value] = step;
                    }
                }

                // 입성 확정 — 야전에서 빠진다(이후 탐지·전투·점유 대상이 아니다).
                foreach (var w in enteringNow)
                {
                    entered.Add(w.Unit.Id);
                    events.Add(new TickEvent(TickEventKind.EnteredCastle, w.Unit.Id, null));
                    work.Remove(w);
                }

                // 동시 이동 해석(같은 칸 경합·자리 맞바꾸기·연쇄·점유 막힘)
                var applied = new Dictionary<int, HexCoord>();
                var engaged = false;
                if (desired.Count > 0)
                {
                    (applied, var moveEvents, engaged) = Resolve(work, desired);
                    events.AddRange(moveEvents);
                }

                foreach (var w in work)
                {
                    if (applied.TryGetValue(w.Unit.Id.Value, out var tile))
                    {
                        w.LastPos = w.Unit.Position; // 되돌아가기 방지용
                        w.Unit = w.Unit.MoveTo(tile);
                        w.MovedToday += TerrainCost(tile); // 진입 지형만큼 이동 예산 차감
                        movedThisDay.Add(w.Unit.Id.Value);
                        // 이번에 밟은 칸은 경로에서 소비한다. 막혀서 못 가면 그대로 둬 다음 스텝에 다시 노린다
                        if (w.Path is { Count: > 0 } && w.Path.Peek() == tile)
                        {
                            w.Path.Dequeue();
                        }
                    }
                }

                // 경유지 도달 판정 — 현재 구간 목표(중간 경유지)에 **닿거나 인접하면**(거리 1 이내) 다음 구간으로.
                // 경유지는 경로 힌트라 정확히 그 칸에 못 서도(점유·통행불가·우회) 근처를 지나면 통과로 친다 —
                // 정확 도달만 요구하면 못 밟는 경유지 근처에서 영영 진동하기 때문. 최종 목표(Goals의 마지막)는 제외.
                foreach (var w in work)
                {
                    while (w.GoalIdx < w.Goals.Count - 1 && w.Unit.Position.Distance(w.Goals[w.GoalIdx]) <= 1)
                    {
                        w.GoalIdx++;
                        w.Path = null; // 다음 구간 경로를 새 위치에서 재계산
                    }
                }

                // 출격 대기 비용(#추가 2026-08-20): 성 위에서 나가려 했지만 앞선 부대에 밀려 못 나간
                // 부대(나갈 칸을 못 잡았거나 desired가 밀린 경우)는 그 대기에 이동 1스텝을 쓴다.
                // 이동력이 남으면 같은 날 뒤따라 나올 수 있고, 다 쓰면 그날은 성에서 대기한다.
                // movedThisDay에 넣어 전체 진행을 멈추는 3일 정체 판정에서 뺀다 — 앞 부대가 계속
                // 나오면 이 부대만 여러 진행을 대기할 뿐 다른 부대의 진행은 막지 않는다.
                foreach (var w in work)
                {
                    var id = w.Unit.Id.Value;
                    var pushedOut = desired.ContainsKey(id) && !applied.ContainsKey(id) && OnCastle(w, castles);
                    if (pushedOut || egressBlocked.Contains(id))
                    {
                        w.MovedToday += 1;
                        movedThisDay.Add(id);
                    }
                }

                // 사건이 있거나 실제로 움직였으면 스냅샷을 남긴다
                if (events.Count > 0 || applied.Count > 0)
                {
                    ticks.Add(Snapshot(day, work, events));
                }

                // 정면 충돌(자리 맞바꾸기·같은 칸)은 즉시 교전한다.
                if (engaged)
                {
                    return Finish(ticks, work, StopReason.Engaged, daysElapsed, entered);
                }

                if (desired.Count == 0 || applied.Count == 0)
                {
                    break; // 이 날은 더 못 간다(전원 사거리 안·막힘·이동력 소진) — 정지 판정으로
                }
            }

            // 진행 중단 — 이 날 이동을 모두 끝낸 뒤, 공격모드 부대의 사거리 안에 적이 있으면 멈춘다
            // (2026-08-12: 접전 순간 즉시 정지 → 그 날 이동 완료 후 정지로 완화. 아군에 잠깐 막혔던
            // 부대도 같은 날 안에 재시도할 수 있어, 접적 순간 얼어붙던 대기가 줄어든다).
            var halter = work.FirstOrDefault(w =>
                w.Unit.Mode == UnitMode.Attack && !OnCastle(w, castles)
                && NearestEnemyWithin(w, work, w.Unit.AttackRange) is not null);
            if (halter is not null)
            {
                var enemy = NearestEnemyWithin(halter, work, halter.Unit.AttackRange);
                ticks.Add(Snapshot(day, work, new[] { new TickEvent(TickEventKind.Halted, halter.Unit.Id, enemy?.Unit.Id) }));
                return Finish(ticks, work, StopReason.EnemyInRange, daysElapsed, entered);
            }

            // 적 성이 공성 사거리 안이어도 같은 규칙으로 그 날 이동 완료 후 진행을 끊는다
            // — 먼저 도착한 부대만 이번 공성 교환에 포함되고, 뒤처진 부대는 다음 진행에 합류한다.
            var besieger = work.FirstOrDefault(w =>
                w.Unit.Mode == UnitMode.Attack && CastleWithin(w, castles) is not null);
            if (besieger is not null)
            {
                ticks.Add(Snapshot(day, work, new[] { new TickEvent(TickEventKind.Halted, besieger.Unit.Id, null) }));
                return Finish(ticks, work, StopReason.CastleInRange, daysElapsed, entered);
            }

            // 3일 연속 못 움직인 유닛 추적(목표가 있는데 못 간 경우만)
            foreach (var w in work)
            {
                var wantsMove = w.Pursuing || (w.CurrentGoal is { } t && t != w.Unit.Position);
                w.BlockedDays = wantsMove && !movedThisDay.Contains(w.Unit.Id.Value) ? w.BlockedDays + 1 : 0;
            }

            if (work.Any(w => w.BlockedDays >= 3))
            {
                reason = StopReason.Blocked;
                return Finish(ticks, work, reason, daysElapsed, entered);
            }
        }

        return Finish(ticks, work, reason, daysElapsed, entered);
    }

    // 목표도 없고 추격도 안 하는(움직일 뜻이 없는) 유닛인가
    private static bool NoIntent(Working w) =>
        !w.Pursuing && (w.CurrentGoal is not { } t || t == w.Unit.Position);

    private int EffectiveSpeed(Working w, List<Working> work)
    {
        if (w.Unit.Mode == UnitMode.March && NearestEnemyWithin(w, work, w.Unit.Detection) is not null)
        {
            return Math.Max(1, w.Unit.Speed - 1);
        }

        return w.Unit.Speed;
    }

    // 지형 이동 패널티(design-movement "지형 이동 보정"): 소형산·늪·소하천 칸에 들어가는 이동은
    // 그날 이동 예산을 1 더 쓴다(전 병종). 진입 칸 기준이라, 이미 그 칸에 서 있다 나가는 건 정상이다.
    // 최소 1칸은 늘 들어갈 수 있어(예산은 이동 후 차감) 속도 1 병종이 묶이지 않는다.
    private int TerrainCost(HexCoord entered) => _passability.TerrainAt(entered) switch
    {
        TerrainType.Mountain or TerrainType.Swamp or TerrainType.River => 2,
        _ => 1,
    };

    // 자신의 공성 사거리 안에 있는 적 성(고정 목록 순서 — 결정론)
    private static SiegeSite? CastleWithin(Working w, IReadOnlyList<SiegeSite>? castles) => castles?
        .FirstOrDefault(c => c.Owner != w.Unit.Owner
            && c.Position.Distance(w.Unit.Position) <= w.Unit.RangeCastle);

    // 성 타일 위에 서 있는가(출격 대기) — 성은 이동 불가 지형이라 머무를 수 없다.
    private static bool OnCastle(Working w, IReadOnlyList<SiegeSite>? castles)
        => castles is not null && castles.Any(c => c.Position == w.Unit.Position);

    // 출격 게이트 스텝: 목표 방향으로 흩어져 나오도록 한다. 후보는 빈·통행이며 이번 스텝에 다른
    // 출격 부대가 찜하지 않은(claimed) 이웃. 적 점유 칸은 후보에서 뺀다(성문 위 교전 금지).
    // ① 목표에서 성 타일보다 멀어지지 않는(dist ≤ 성→목표) 후보 중 가장 가까운 칸 — 여러 부대가
    //    6방향 중 목표 쪽 칸들을 나눠 갖고, 뒤로 돌아 나가지는 않는다. ② 그런 칸이 없고 아직 아무도
    //    나가지 않았으면(이 스텝 첫 출격 부대·포위 등) 뒤 칸이라도 가장 가까운 빈 칸으로 내려선다
    //    — 성 위에 갇히지 않도록. ③ 둘 다 없으면 null(대기).
    private HexCoord? GateStep(Working w, HexCoord goal, HashSet<HexCoord> occupied, HashSet<HexCoord> claimed)
    {
        var here = w.Unit.Position;
        var hereDist = here.Distance(goal);

        HexCoord? best = null;
        var bestDist = int.MaxValue;
        foreach (var n in here.Neighbors())
        {
            if (!occupied.Contains(n) && !claimed.Contains(n) && _passability.CanEnter(w.Unit.Domain, n)
                && n.Distance(goal) <= hereDist && n.Distance(goal) < bestDist)
            {
                best = n;
                bestDist = n.Distance(goal);
            }
        }

        if (best is not null || claimed.Count > 0)
        {
            return best; // 뒤 부대는 목표 쪽 빈 칸이 없으면 대기(뒤로 돌아 나가지 않는다)
        }

        // 이 스텝 첫 출격 부대: 목표 쪽 칸이 없어도(완전 포위 근처 등) 가장 가까운 빈 칸으로 내려선다.
        foreach (var n in here.Neighbors())
        {
            if (!occupied.Contains(n) && _passability.CanEnter(w.Unit.Domain, n) && n.Distance(goal) < bestDist)
            {
                best = n;
                bestDist = n.Distance(goal);
            }
        }

        return best;
    }

    // 목표 없는 출격 게이트 스텝: 빈·통행이며 아직 안 찜한(claimed) 이웃 중 고정 방향 순서 첫 칸.
    // 여러 부대는 서로 다른 이웃으로 흩어져 성 앞에 대기한다.
    private HexCoord? GateStepAny(Working w, HashSet<HexCoord> occupied, HashSet<HexCoord> claimed)
    {
        foreach (var n in w.Unit.Position.Neighbors())
        {
            if (!occupied.Contains(n) && !claimed.Contains(n) && _passability.CanEnter(w.Unit.Domain, n))
            {
                return n;
            }
        }

        return null;
    }

    // 다음 스텝 칸이 자기 성인가 — 입성 조건. 성 타일은 통행 불가지만 경로는 목표 칸을 허용하므로,
    // 목표가 자기 성인 유닛의 마지막 스텝만 여기 걸린다(추격·통과 경로는 성 타일을 지나지 않는다).
    private static bool IsOwnCastle(Working w, HexCoord next, IReadOnlyList<SiegeSite>? castles)
        => castles is not null
            && w.Unit.Target == next
            && castles.Any(c => c.Owner == w.Unit.Owner && c.Position == next);

    // 사거리·탐지 안의 적 중 가장 가까운 하나(동률이면 명령 순번, 그다음 UnitId — 결정론)
    private static Working? NearestEnemyWithin(Working self, List<Working> work, int range) => work
        .Where(o => o.Unit.Owner != self.Unit.Owner
            && o.Unit.Position.Distance(self.Unit.Position) <= range)
        .OrderBy(o => o.Unit.Position.Distance(self.Unit.Position))
        .ThenBy(o => o.Unit.CommandOrder)
        .ThenBy(o => o.Unit.Id.Value)
        .FirstOrDefault();

    // 경로의 다음 칸이 다른 유닛에 막혀 있으면 한 스텝 국소 우회한다(전체 A*를 다시 돌리지 않아
    // 결정론·성능 유지 — design-movement "재계산 없이"의 절충). 아군 뒤에 갇힌 부대가 돌아 교전할 수
    // 있게 한다. ① 목표에 더 가까운 빈·통행 이웃(직진 우회) → ② 없으면 목표와 같은 거리의 빈·통행
    // 이웃(측면 우회, 직전 칸 제외로 진동 방지). 둘 다 없으면(완전 포위) 원래 칸을 반환해 대기시킨다.
    // 이웃은 고정 방향 순서로 봐 결정론을 지킨다.
    private HexCoord StepOrDetour(Working w, HexCoord next, HexCoord? goal, HashSet<HexCoord> occupied,
        Dictionary<HexCoord, int> occupantOwner)
    {
        if (!occupied.Contains(next) || goal is not { } g)
        {
            return next;
        }

        // 적이 막고 있으면 우회하지 않는다 — 자리 맞바꾸기 교전·연쇄 이동·점유 정지 규칙에 맡긴다.
        // 아군이 막을 때만 돌아간다.
        if (occupantOwner.TryGetValue(next, out var owner) && owner != w.Unit.Owner.Value)
        {
            return next;
        }

        var here = w.Unit.Position;
        var hereDist = here.Distance(g);

        // ① 직진 우회 — 목표에 더 가까운 빈 칸
        foreach (var n in here.Neighbors())
        {
            if (!occupied.Contains(n) && n.Distance(g) < hereDist && _passability.CanEnter(w.Unit.Domain, n))
            {
                return n;
            }
        }

        // ② 측면 우회 — 같은 거리의 빈 칸(직전 칸으로 되돌아가지 않는다)
        foreach (var n in here.Neighbors())
        {
            if (!occupied.Contains(n) && n.Distance(g) == hereDist && n != w.LastPos && _passability.CanEnter(w.Unit.Domain, n))
            {
                return n;
            }
        }

        return next;
    }

    // 현재 위치에서 goal까지의 남은 경로(시작 칸 제외)를 큐로 만든다.
    // 지형 통행만 본다 — 유닛 점유는 스텝 해석에서 다룬다. 재계산해도 결정적(A*).
    private Queue<HexCoord> BuildPath(Working w, HexCoord goal)
    {
        var domain = w.Unit.Domain;
        var start = w.Unit.Position;
        var pathfinder = new HexPathfinder(c =>
            c == start || c == goal || _passability.CanEnter(domain, c));
        var path = pathfinder.FindPath(start, goal);
        var queue = new Queue<HexCoord>();
        for (var i = 1; i < path.Count; i++)
        {
            queue.Enqueue(path[i]);
        }

        return queue;
    }

    // 동시 이동 해석: 같은 칸 경합·자리 맞바꾸기·점유 칸 막힘을 풀고, 적끼리면 교전.
    // 연쇄 이동(a→b, b→c)은 비우는 쪽이 먼저 정리되도록 고정점 반복으로 성립시킨다.
    private (Dictionary<int, HexCoord> Applied, List<TickEvent> Events, bool Engaged) Resolve(
        List<Working> work, Dictionary<int, HexCoord> desired)
    {
        var applied = new Dictionary<int, HexCoord>(desired);
        var events = new List<TickEvent>();
        var engaged = false;
        var byId = work.ToDictionary(w => w.Unit.Id.Value);

        bool changed;
        do
        {
            changed = false;

            // 자리 맞바꾸기(a→b, b→a)
            foreach (var a in work)
            {
                if (!applied.TryGetValue(a.Unit.Id.Value, out var aTo))
                {
                    continue;
                }

                foreach (var b in work)
                {
                    if (a.Unit.Id.Value >= b.Unit.Id.Value
                        || !applied.TryGetValue(b.Unit.Id.Value, out var bTo))
                    {
                        continue;
                    }

                    if (aTo == b.Unit.Position && bTo == a.Unit.Position)
                    {
                        applied.Remove(a.Unit.Id.Value);
                        applied.Remove(b.Unit.Id.Value);
                        changed = true;
                        if (a.Unit.Owner != b.Unit.Owner)
                        {
                            engaged = true;
                            events.Add(new TickEvent(TickEventKind.Engaged, a.Unit.Id, b.Unit.Id));
                        }
                    }
                }
            }

            // 같은 칸 경합(둘 이상이 한 칸을 노림): 명령 순번이 앞선 유닛이 그 칸을
            // 차지하고 나머지는 막힌다(2026-08-08 사용자 정의). 아무도 못 들어가면 둘이
            // 한 칸 벌어진 채 멈춰 사거리 밖에서 헛교전이 되므로, 우선순위로 밀어 넣어
            // 인접하게 만든 뒤 사거리 규칙에 맡긴다.
            foreach (var group in work
                .Where(w => applied.ContainsKey(w.Unit.Id.Value))
                .GroupBy(w => applied[w.Unit.Id.Value])
                .Where(g => g.Count() > 1))
            {
                var losers = group
                    .OrderBy(w => w.Unit.CommandOrder)
                    .ThenBy(w => w.Unit.Id.Value)
                    .Skip(1);
                foreach (var w in losers)
                {
                    applied.Remove(w.Unit.Id.Value);
                }

                changed = true;
            }

            // 점유 칸 막힘(비우지 않는 유닛이 있는 칸으로는 못 들어간다)
            foreach (var w in work)
            {
                if (!applied.TryGetValue(w.Unit.Id.Value, out var to))
                {
                    continue;
                }

                var occupant = work.FirstOrDefault(o =>
                    o.Unit.Id.Value != w.Unit.Id.Value
                    && o.Unit.Position == to
                    && !applied.ContainsKey(o.Unit.Id.Value));
                if (occupant is null)
                {
                    continue;
                }

                applied.Remove(w.Unit.Id.Value);
                changed = true;
                if (occupant.Unit.Owner != w.Unit.Owner)
                {
                    engaged = true;
                    events.Add(new TickEvent(TickEventKind.Engaged, w.Unit.Id, occupant.Unit.Id));
                }
            }
        }
        while (changed);

        return (applied, events, engaged);
    }

    private static MovementTick Snapshot(int day, List<Working> work, IReadOnlyList<TickEvent> events) =>
        new(day, work.OrderBy(w => w.Unit.Id.Value).Select(w => w.Unit).ToList(), events);

    private static AdvanceResult Finish(
        List<MovementTick> ticks, List<Working> work, StopReason reason, int days, List<UnitId> entered) =>
        new(ticks, work.OrderBy(w => w.Unit.Id.Value).Select(TrimWaypoints).ToList(), reason, days,
            entered.OrderBy(id => id.Value).ToList());

    // 소비한 경유지를 결과 부대에서 잘라낸다 — 안 그러면 다음 진행에 경로를 처음부터 다시 밟아 되돌아간다.
    // 남은 경유지 = 현재 구간 목표부터 최종 목표 직전까지(최종 목표는 Target으로 유지).
    private static FieldUnit TrimWaypoints(Working w)
    {
        if (w.Unit.Waypoints is null or { Count: 0 }) { return w.Unit; }
        var remainingCount = System.Math.Max(0, w.Goals.Count - 1 - w.GoalIdx);
        var remaining = w.Goals.Skip(w.GoalIdx).Take(remainingCount).ToList();
        return w.Unit with { Waypoints = remaining.Count > 0 ? remaining : null };
    }
}
