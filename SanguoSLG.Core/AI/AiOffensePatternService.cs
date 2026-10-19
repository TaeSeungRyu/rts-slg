namespace SanguoSLG.Core.AI;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

public enum AiOffensePattern
{
    None,
    RegularSiege,
    ArmyGroupSiege,
    CavalrySiege,
}

/// <summary>공세 편성 전의 병종·거리 사전 검사. 전투 모의와 지출 예약은 별도 단계다.</summary>
public sealed record AiOffensePatternChoice(AiOffensePattern Pattern, bool RequiresSupply, string Reason);

public static class AiOffensePatternService
{
    public static AiOffensePatternChoice Choose(GameState state, City origin, City target,
        IReadOnlyDictionary<string, TroopTemplate> troops, int freeGenerals)
    {
        var assessment = AiOffensiveCandidateService.Evaluate(state, origin, target);
        if (!assessment.PassesForceGate)
            return new(AiOffensePattern.None, false, assessment.Reason);

        var available = state.Garrisons.Where(g => g.City == origin.Id && !g.Trainee && g.Troops > 0)
            .Where(g => troops.ContainsKey(g.TroopCode))
            .GroupBy(g => troops[g.TroopCode].Class)
            .ToDictionary(group => group.Key, group => group.Sum(g => g.Troops));
        int Count(TroopClass kind) => available.GetValueOrDefault(kind);
        var armyGroupPool = Count(TroopClass.Infantry) + Count(TroopClass.Archer) + Count(TroopClass.Siege);
        if (assessment.DefenderTroops >= 50_000 && state.Garrisons.Where(g => g.City == origin.Id && !g.Trainee)
                .Sum(g => g.Troops) >= 50_000 && armyGroupPool >= 30_000 && freeGenerals >= 4)
            return new(AiOffensePattern.ArmyGroupSiege, true, "집단군 공성 후보");

        if (Count(TroopClass.Siege) <= 0)
            return new(AiOffensePattern.None, false, "공성무기가 없습니다.");

        if (assessment.Distance < 15 && Count(TroopClass.Cavalry) >= 10_000)
            return new(AiOffensePattern.CavalrySiege, false, "단거리 기병 기동 공성 후보");

        return new(AiOffensePattern.RegularSiege, true, "정규 공성 후보");
    }
}
