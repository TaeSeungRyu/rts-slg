namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>
/// 내정 스킬(태수 패시브) 버킷 합산(design-skill-admin.md). 한 장수가 가진 내정 패시브 중
/// 주어진 버킷에 해당하는 것들의 티어 수치를 더한다. WorldEngine(수입·치안·산출)과
/// CommandService(모병·훈련·수리 명령)가 공유한다 — 담당관 스킬 계산은 게임 전체에 하나다.
/// </summary>
public static class AdminBonus
{
    public static int Bucket(General? governor, IReadOnlyDictionary<string, AdminSkill> skills, string bucket)
    {
        if (governor is null)
        {
            return 0;
        }

        var sum = 0;
        foreach (var held in governor.AdminPassives ?? [])
        {
            if (skills.TryGetValue(held.Code, out var def) && def.Bucket == bucket)
            {
                sum += def.AmountAtTier(held.Tier);
            }
        }

        return sum;
    }
}
