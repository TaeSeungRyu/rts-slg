namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

/// <summary>야전 전멸 부대의 장수 귀환 결과. 복귀 도시가 없으면 재야로 남는다.</summary>
public sealed record CasualtyReport(UnitId Unit, GeneralId General, CityId? Refuge);

/// <summary>
/// 야전 전멸 시 장수 처리. 포로 시스템은 폐기되었으므로 선봉·부관은 항상 귀환한다.
/// 편성 원점이 아직 아군 소유면 그곳을 우선하고, 불가능하면 전멸 지점에서 가장 가까운
/// 아군 도시로 복귀한다. 보유 도시가 없으면 배속만 해제해 재야로 보존한다.
/// </summary>
public static class FieldCasualties
{
    public static GameState ResolveUnit(GameState state, CombatUnit dead, HexCoord at,
        List<CasualtyReport> reports)
    {
        foreach (var gid in new[] { dead.VanguardId, dead.AdjutantId }.OfType<GeneralId>().Distinct())
        {
            var general = gid;
            var refuge = dead.OriginCity is { } origin
                ? state.Cities.FirstOrDefault(c => c.Id == origin && c.Owner == dead.Field.Owner)
                : null;
            refuge ??= state.Cities.Where(c => c.Owner == dead.Field.Owner)
                .OrderBy(c => c.Position.Distance(at)).ThenBy(c => c.Id.Value).FirstOrDefault();
            var postings = state.Assignments.Where(p => p.General != general).ToList();
            if (refuge is not null)
            {
                postings.Add(new GeneralPosting(general, dead.Field.Owner, refuge.Id));
                state = state with { Postings = postings };
                reports.Add(new CasualtyReport(dead.Id, general, refuge.Id));
            }
            else
            {
                state = state with { Postings = postings };
                reports.Add(new CasualtyReport(dead.Id, general, Refuge: null));
            }
        }

        return state;
    }
}
