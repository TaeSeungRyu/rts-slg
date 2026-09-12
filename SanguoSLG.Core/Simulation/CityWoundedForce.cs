namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>도시 주둔 수비대의 부상병 풀. 공성 피해 일부가 쌓이고, 수성 중이 아닐 때만 회복된다.</summary>
public sealed record CityWoundedForce(CityId City, string TroopCode, int Troops, int TrainingLevel, bool Trainee = false)
{
    public CityWoundedForce Merge(int troops, int trainingLevel)
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
