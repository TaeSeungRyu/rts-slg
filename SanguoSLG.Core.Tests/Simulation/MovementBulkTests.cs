namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>
/// 다수 부대 대량 이동의 사이드 이펙트 점검(2026-08-12). 특정 좌표가 아니라 규모에 상관없이
/// 지켜져야 할 불변식을 단언한다 — 결정론(입력 순서 무관), 타일 중복 없음, 부대 보존, 맵 이탈 없음,
/// 하루 속도 상한. 결함이 있으면 규모가 커질 때 여기서 드러난다.
/// </summary>
public class MovementBulkTests
{
    private static readonly HexMap Map = new(0, 30, 0, 10);

    private static MovementSimulator Sim() => new(new PassabilityMap(Map, [], []));

    private static FieldUnit Unit(int id, int owner, HexCoord pos, UnitMode mode, HexCoord? target,
        int speed = 2, int detection = 3, int attackRange = 1) =>
        new(new UnitId(id), new FactionId(owner), pos, speed, detection, attackRange,
            MovementDomain.Land, mode, target, id);

    // 두 진영이 각자 레인(행)을 따라 마주 진격하는 대군. 인접 행끼리도 사거리가 겹쳐 교차 교전·경합이
    // 생기므로 충돌 해석을 규모로 압박한다.
    private static List<FieldUnit> Armies(int perSide)
    {
        var units = new List<FieldUnit>();
        for (var i = 0; i < perSide; i++)
        {
            units.Add(Unit(100 + i, 1, new HexCoord(0, i), UnitMode.Attack, new HexCoord(28, i)));
            units.Add(Unit(200 + i, 2, new HexCoord(28, i), UnitMode.Attack, new HexCoord(0, i)));
        }

        return units;
    }

    [Fact]
    public void 대량_입력순서를_뒤집어도_결과가_같다()
    {
        var units = Armies(8);
        var forward = Sim().Advance(units);
        var reversed = Sim().Advance(units.AsEnumerable().Reverse().ToList());

        var f = forward.Units.ToDictionary(u => u.Id.Value, u => u.Position);
        var r = reversed.Units.ToDictionary(u => u.Id.Value, u => u.Position);

        Assert.Equal(f.Count, r.Count);
        foreach (var (id, pos) in f)
        {
            Assert.Equal(pos, r[id]);
        }

        Assert.Equal(forward.Reason, reversed.Reason);
        Assert.Equal(forward.Days, reversed.Days);
    }

    [Fact]
    public void 대량_어느틱에도_두부대가_같은칸에_있지_않는다()
    {
        var result = Sim().Advance(Armies(8));

        foreach (var tick in result.Ticks)
        {
            var positions = tick.Units.Select(u => u.Position).ToList();
            Assert.Equal(positions.Count, positions.Distinct().Count());
        }

        var finals = result.Units.Select(u => u.Position).ToList();
        Assert.Equal(finals.Count, finals.Distinct().Count());
    }

    [Fact]
    public void 대량_부대는_하나도_사라지거나_늘지_않는다()
    {
        var input = Armies(8);
        var result = Sim().Advance(input);

        Assert.Equal(input.Count, result.Units.Count);
        Assert.Equal(
            input.Select(u => u.Id.Value).OrderBy(x => x),
            result.Units.Select(u => u.Id.Value).OrderBy(x => x));
    }

    [Fact]
    public void 대량_모든_위치는_맵_경계_안에_머문다()
    {
        var result = Sim().Advance(Armies(8));

        foreach (var tick in result.Ticks)
        {
            Assert.All(tick.Units, u => Assert.True(Map.Contains(u.Position), $"{u.Id.Value} @ {u.Position}"));
        }

        Assert.All(result.Units, u => Assert.True(Map.Contains(u.Position)));
    }

    [Fact]
    public void 대량_어느_부대도_하루에_속도를_넘겨_이동하지_않는다()
    {
        const int speed = 2;
        var input = Armies(8);
        var result = Sim().Advance(input);

        var movesPerDay = new Dictionary<(int Id, int Day), int>();
        var lastPos = input.ToDictionary(u => u.Id.Value, u => u.Position);
        foreach (var tick in result.Ticks)
        {
            foreach (var u in tick.Units)
            {
                if (lastPos[u.Id.Value] != u.Position)
                {
                    var key = (u.Id.Value, tick.Day);
                    movesPerDay[key] = movesPerDay.GetValueOrDefault(key) + 1;
                    lastPos[u.Id.Value] = u.Position;
                }
            }
        }

        Assert.All(movesPerDay.Values, v => Assert.True(v <= speed, $"하루 {v}칸 이동 > 속도 {speed}"));
    }

    [Fact]
    public void 대량_규모가_달라도_불변식은_유지된다()
    {
        // 규모를 키워도 종료하고(교착·무한루프 없음) 겹침이 없는지 확인.
        foreach (var perSide in new[] { 2, 5, 10 })
        {
            var result = Sim().Advance(Armies(perSide));
            Assert.Equal(perSide * 2, result.Units.Count);
            var finals = result.Units.Select(u => u.Position).ToList();
            Assert.Equal(finals.Count, finals.Distinct().Count());
        }
    }
}
