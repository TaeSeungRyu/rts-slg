namespace SanguoSLG.Core.AI;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

/// <summary>공세 모의 교전 이전의 보수적 후보 필터. 통과해도 출전 허가는 아니다.</summary>
public sealed record AiOffensiveCandidate(
    CityId Origin, CityId Target, int AvailableTroops, int DefenderTroops,
    int ReserveTroops, int Distance, bool PassesForceGate, string Reason);

public static class AiOffensiveCandidateService
{
    public static AiOffensiveCandidate Evaluate(GameState state, City origin, City target,
        int minimumReserve = 10_000, int requiredRatioPercent = 120)
    {
        if (origin.Owner == target.Owner || state.AreAllied(origin.Owner, target.Owner))
            return new(origin.Id, target.Id, 0, 0, 0, origin.Position.Distance(target.Position),
                false, "적대하지 않는 성은 공격 대상이 아닙니다.");

        var stationed = state.Garrisons.Where(g => g.City == origin.Id && !g.Trainee)
            .Sum(g => Math.Max(0, g.Troops));
        var defenders = state.Garrisons.Where(g => g.City == target.Id)
            .Sum(g => Math.Max(0, g.Troops));
        var reserve = Math.Max(minimumReserve, (int)Math.Ceiling(stationed * 0.30));
        var available = Math.Max(0, stationed - reserve);
        var required = (int)Math.Ceiling(defenders * requiredRatioPercent / 100.0);
        var passes = available > 0 && available >= required;
        return new(origin.Id, target.Id, available, defenders, reserve,
            origin.Position.Distance(target.Position), passes,
            passes ? "모의 교전 평가 필요" : "방어 예비를 제외한 공세 병력이 부족합니다.");
    }
}
