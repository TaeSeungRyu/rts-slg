namespace SanguoSLG.Core.Domain;

/// <summary>
/// 도시 대기 병력(병종별) — 모집 시 병종을 지정하므로(2026-08-16 확정) 대기 병력이 병종 단위로
/// 쌓인다. City 레코드에 컬렉션을 넣으면 값 동등성(결정론 검증)이 깨져 GameState의 별도 목록으로 둔다.
/// </summary>
public sealed record GarrisonForce(CityId City, string TroopCode, int Troops, int TrainingLevel)
{
    /// <summary>병력 합류 — 훈련도는 가중 평균·정수 반올림(design-unit-state "보충 희석").</summary>
    public GarrisonForce Merge(int troops, int trainingLevel)
    {
        if (troops <= 0)
        {
            return this;
        }

        var total = Troops + troops;
        var sum = Troops * (long)TrainingLevel + troops * (long)trainingLevel;
        var blended = (int)((sum + total / 2) / total);
        return this with { Troops = total, TrainingLevel = blended };
    }
}
