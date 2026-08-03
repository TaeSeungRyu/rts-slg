using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using Xunit;

namespace SanguoSLG.Core.Tests.Simulation;

// 실제 시나리오 데이터로 데이터→맵→A*→이동 전체 경로를 관통 검증한다.
public class MovementIntegrationTests
{
    [Fact]
    public void 실제_맵에서_한_도시에서_다른_도시로_이동해_도착한다()
    {
        var scenario = new ScenarioLoader().LoadFromDirectory(TestData.DataDirectory());
        var service = new MovementService(scenario.Map);

        var from = scenario.Cities[0];                    // 허창
        var to = scenario.Cities[^1];                     // 형주
        var unit = new Unit(new UnitId(1), from.Owner, from.Position);

        var result = service.MoveTo(unit, to.Position);

        Assert.True(result.Moved);
        Assert.Equal(to.Position, result.Unit.Position);
        Assert.Equal(from.Position, result.Path[0]);
        Assert.Equal(to.Position, result.Path[^1]);
        // 경로의 모든 칸이 맵 안이다.
        Assert.All(result.Path, tile => Assert.True(scenario.Map.Contains(tile)));
    }
}
