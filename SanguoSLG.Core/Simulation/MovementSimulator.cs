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

    private sealed class Working
    {
        public FieldUnit Unit;
        public int MovedToday;
        public bool Pursuing;
        public int BlockedDays;

        public Working(FieldUnit unit) => Unit = unit;
    }

    /// <summary>한 번의 "진행"을 끝까지 계산한다(최대 <paramref name="maxDays"/>일).</summary>
    public AdvanceResult Advance(IReadOnlyList<FieldUnit> units, int maxDays = 7)
    {
        var work = units.OrderBy(u => u.Id.Value).Select(u => new Working(u)).ToList();
        var ticks = new List<MovementTick>();
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
                // 진행 중단 1 — 공격모드 유닛의 사거리 안에 적이 들어왔다
                var halter = work.FirstOrDefault(w =>
                    w.Unit.Mode == UnitMode.Attack && NearestEnemyWithin(w, work, w.Unit.AttackRange) is not null);
                if (halter is not null)
                {
                    var enemy = NearestEnemyWithin(halter, work, halter.Unit.AttackRange);
                    ticks.Add(Snapshot(day, work,
                        new[] { new TickEvent(TickEventKind.Halted, halter.Unit.Id, enemy?.Unit.Id) }));
                    return Finish(ticks, work, StopReason.EnemyInRange, daysElapsed);
                }

                // 진행 중단 2 — 아무도 더 갈 곳이 없다(전원 목표 도착)
                if (work.All(NoIntent))
                {
                    reason = StopReason.AllArrived;
                    return Finish(ticks, work, reason, daysElapsed);
                }

                var events = new List<TickEvent>();

                // 공격모드 유닛의 추격 상태를 갱신한다(탐지 시작/시야 상실)
                foreach (var w in work.Where(w => w.Unit.Mode == UnitMode.Attack))
                {
                    var seen = NearestEnemyWithin(w, work, w.Unit.Detection);
                    if (seen is not null && !w.Pursuing)
                    {
                        w.Pursuing = true;
                        events.Add(new TickEvent(TickEventKind.PursuitStarted, w.Unit.Id, seen.Unit.Id));
                    }
                    else if (seen is null && w.Pursuing)
                    {
                        w.Pursuing = false;
                        events.Add(new TickEvent(TickEventKind.PursuitEnded, w.Unit.Id, null));
                    }
                }

                // 이번 스텝에 움직이려는 유닛의 희망 칸을 모은다
                var desired = new Dictionary<int, HexCoord>();
                foreach (var w in work)
                {
                    if (w.MovedToday >= EffectiveSpeed(w, work))
                    {
                        continue;
                    }

                    var goal = w.Pursuing
                        ? NearestEnemyWithin(w, work, w.Unit.Detection)?.Unit.Position
                        : w.Unit.Target;
                    if (goal is null || goal == w.Unit.Position)
                    {
                        continue;
                    }

                    if (NextStep(w, goal.Value) is { } next)
                    {
                        desired[w.Unit.Id.Value] = next;
                    }
                }

                if (desired.Count == 0)
                {
                    // 못 움직여도 이번 스텝에 사건(추격 시작·시야 상실)이 있으면 남긴다 —
                    // 그러지 않으면 속도를 다 쓴 날 끝에 걸린 탐지가 조용히 사라진다
                    if (events.Count > 0)
                    {
                        ticks.Add(Snapshot(day, work, events));
                    }

                    break; // 이 날은 더 못 간다 — 다음 날로
                }

                var (applied, moveEvents, engaged) = Resolve(work, desired);
                events.AddRange(moveEvents);

                foreach (var w in work)
                {
                    if (applied.TryGetValue(w.Unit.Id.Value, out var tile))
                    {
                        w.Unit = w.Unit.MoveTo(tile);
                        w.MovedToday++;
                        movedThisDay.Add(w.Unit.Id.Value);
                    }
                }

                ticks.Add(Snapshot(day, work, events));

                if (engaged)
                {
                    return Finish(ticks, work, StopReason.Engaged, daysElapsed);
                }

                if (applied.Count == 0)
                {
                    break; // 전원이 막혔다 — 다음 날로
                }
            }

            // 3일 연속 못 움직인 유닛 추적(목표가 있는데 못 간 경우만)
            foreach (var w in work)
            {
                var wantsMove = w.Pursuing || (w.Unit.Target is { } t && t != w.Unit.Position);
                w.BlockedDays = wantsMove && !movedThisDay.Contains(w.Unit.Id.Value) ? w.BlockedDays + 1 : 0;
            }

            if (work.Any(w => w.BlockedDays >= 3))
            {
                reason = StopReason.Blocked;
                return Finish(ticks, work, reason, daysElapsed);
            }
        }

        return Finish(ticks, work, reason, daysElapsed);
    }

    // 목표도 없고 추격도 안 하는(움직일 뜻이 없는) 유닛인가
    private static bool NoIntent(Working w) =>
        !w.Pursuing && (w.Unit.Target is not { } t || t == w.Unit.Position);

    private int EffectiveSpeed(Working w, List<Working> work)
    {
        if (w.Unit.Mode == UnitMode.March && NearestEnemyWithin(w, work, w.Unit.Detection) is not null)
        {
            return Math.Max(1, w.Unit.Speed - 1);
        }

        return w.Unit.Speed;
    }

    // 사거리·탐지 안의 적 중 가장 가까운 하나(동률이면 명령 순번, 그다음 UnitId — 결정론)
    private static Working? NearestEnemyWithin(Working self, List<Working> work, int range) => work
        .Where(o => o.Unit.Owner != self.Unit.Owner
            && o.Unit.Position.Distance(self.Unit.Position) <= range)
        .OrderBy(o => o.Unit.Position.Distance(self.Unit.Position))
        .ThenBy(o => o.Unit.CommandOrder)
        .ThenBy(o => o.Unit.Id.Value)
        .FirstOrDefault();

    private HexCoord? NextStep(Working w, HexCoord goal)
    {
        var domain = w.Unit.Domain;
        var start = w.Unit.Position;
        var pathfinder = new HexPathfinder(c =>
            c == start || c == goal || _passability.CanEnter(domain, c));
        var path = pathfinder.FindPath(start, goal);
        return path.Count >= 2 ? path[1] : null;
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
        List<MovementTick> ticks, List<Working> work, StopReason reason, int days) =>
        new(ticks, work.OrderBy(w => w.Unit.Id.Value).Select(w => w.Unit).ToList(), reason, days);
}
