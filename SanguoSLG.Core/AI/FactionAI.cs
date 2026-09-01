namespace SanguoSLG.Core.AI;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

/// <summary>
/// 세력 AI 최소판(12단계 "모집·출전·공성 판단"). 한 세력의 한 주 결정을 결정론적으로 내린다:
/// ① 야전 공격 부대를 가장 가까운 적 성으로 재조준(멈춘·무효 목표 복구) → ② 도시별(id순) 장수
/// 1명으로 — 대기 병력이 문턱 이상이고 도시에 장수가 남으면 최근접 적 성으로 대군 출전, 아니면
/// 여력만큼 모집. 관전 캠페인에서 수렴을 검증한 휴리스틱을 Core로 승격했다. 정식 확장(내정·연구·
/// 방어 판단)은 후속. 결정론: 세력·도시 id순·문턱값, 난수 없음(함락 판정만 상위 시드 난수).
/// </summary>
public sealed class FactionAI
{
    private readonly DeployService _deployer;
    private readonly AiConfig _config;

    public FactionAI(CommandService commands, DeployService deployer, AiConfig? config = null)
    {
        _ = commands;
        _deployer = deployer;
        _config = config ?? new AiConfig();
    }

    /// <summary>이 세력의 한 주 명령·출전을 반영한 새 상태를 반환한다.</summary>
    public GameState PlanWeek(GameState state, FactionId faction)
    {
        state = Retarget(state, faction);

        foreach (var city in state.Cities.Where(c => c.Owner == faction).OrderBy(c => c.Id.Value).ToList())
        {
            var free = state.GeneralsAt(city.Id)
                .Where(g => !state.IsGeneralBusy(g))
                .OrderBy(g => g.Value)
                .ToList();
            if (free.Count == 0)
            {
                continue;
            }

            var gid = free[0];
            var garrison = state.Garrisons
                .Where(g => g.City == city.Id && g.TroopCode == _config.Troop)
                .Sum(g => g.Troops);

            if (garrison >= _config.DeployTarget && free.Count > _config.KeepGeneralsHome)
            {
                var target = NearestEnemyCity(state, faction, city.Position);
                if (target is { } dest)
                {
                    var result = _deployer.Deploy(state, new DeployRequest(
                        city.Id, _config.Troop, System.Math.Min(garrison, _config.DeploySize),
                        gid, Mode: UnitMode.Attack, Target: dest));
                    if (result.Ok)
                    {
                        state = result.State;
                    }
                }
            }
        }

        return state;
    }

    // 야전 공격 부대를 가장 가까운 적 성으로 재조준(멈춘 부대·무효 목표 복구).
    private static GameState Retarget(GameState state, FactionId faction)
    {
        var armies = state.Armies.Select(u =>
        {
            if (u.Field.Owner != faction || u.Field.Mode != UnitMode.Attack)
            {
                return u;
            }

            var target = NearestEnemyCity(state, faction, u.Field.Position);
            return target is { } dest ? u with { Field = u.Field with { Target = dest } } : u;
        }).ToList();
        return state with { FieldArmies = armies };
    }

    private static HexCoord? NearestEnemyCity(GameState state, FactionId self, HexCoord from)
        => state.Cities.Where(c => c.Owner != self)
            .OrderBy(c => c.Position.Distance(from)).ThenBy(c => c.Id.Value)
            .Select(c => (HexCoord?)c.Position)
            .FirstOrDefault();
}
