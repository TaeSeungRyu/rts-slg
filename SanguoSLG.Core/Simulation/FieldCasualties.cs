namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

/// <summary>야전 전멸 부대의 장수 1명 처리 결과.</summary>
/// <param name="Unit">전멸한 부대.</param>
/// <param name="General">판정된 장수(선봉 또는 부관).</param>
/// <param name="Captured">포로가 됐는가(false = 탈출).</param>
/// <param name="Holder">포로일 때 억류 세력.</param>
/// <param name="Refuge">탈출일 때 복귀한 도시(null = 보유 도시 없음 → 재야).</param>
public sealed record CasualtyReport(UnitId Unit, GeneralId General, bool Captured, FactionId? Holder, CityId? Refuge);

/// <summary>
/// 야전 전멸 시 장수 처리(design-general-lifecycle §4b). 부대가 병력 0으로 소멸하면 선봉·부관을
/// 장수별 개별 판정한다: 교전 상대(<c>captor</c>)가 있으면 50% 포로 / 50% 탈출(원 세력 최근접
/// 보유 도시로 즉시 복귀 — 1차 구현은 즉시), 교전 상대가 없으면(굶주림·지속 피해) 100% 탈출.
/// 보유 도시가 없으면 재야(배속 해제 — 세력 소멸 판정은 §3 상위 규칙 몫).
/// 결정론: 판정은 시드 난수만, 장수 순서는 선봉 → 부관.
/// </summary>
public static class FieldCasualties
{
    public static GameState ResolveUnit(GameState state, CombatUnit dead, FactionId? captor, HexCoord at,
        IRandomSource random, List<CasualtyReport> reports)
    {
        foreach (var gid in new[] { dead.VanguardId, dead.AdjutantId })
        {
            if (gid is not { } general)
            {
                continue;
            }

            if (captor is { } holder && random.Next(0, 2) == 0)
            {
                state = FactionLifecycle.MakePrisoner(state, general, holder, origin: dead.Field.Owner);
                reports.Add(new CasualtyReport(dead.Id, general, Captured: true, holder, Refuge: null));
                continue;
            }

            var refuge = state.Cities.Where(c => c.Owner == dead.Field.Owner)
                .OrderBy(c => c.Position.Distance(at)).ThenBy(c => c.Id.Value)
                .FirstOrDefault();
            if (refuge is not null)
            {
                var postings = state.Assignments
                    .Select(p => p.General == general ? p with { Location = refuge.Id } : p)
                    .ToList();
                state = state with { Postings = postings };
                reports.Add(new CasualtyReport(dead.Id, general, Captured: false, Holder: null, refuge.Id));
            }
            else
            {
                var postings = state.Assignments.Where(p => p.General != general).ToList();
                state = state with { Postings = postings };
                reports.Add(new CasualtyReport(dead.Id, general, Captured: false, Holder: null, Refuge: null));
            }
        }

        return state;
    }
}
