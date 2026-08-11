namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 전투 액티브 스킬 정의(design-skill-actives.md). 수치는 무력/지력 60 기준이며 실제 위력은
/// <see cref="StatScale"/>가 곱해진다. 데이터는 data/active-skills.json에서 온다.
/// (연환격 2대상 분산·맹호격 반격감소·일기당천 무력차식·계략은 후속/별도.)
/// </summary>
/// <param name="Code">스킬 코드.</param>
/// <param name="Name">한국어 이름.</param>
/// <param name="Type">타격/방어/회복.</param>
/// <param name="Grade">"high"/"low".</param>
/// <param name="DamageMultPercent">타격 배수(정상 공격 피해 대비, 무쌍 160).</param>
/// <param name="DefenderDfReductionPercent">타격 시 대상 df 감소(일섬 30, 파갑 20).</param>
/// <param name="ExecutePercent">병력 비례 처형 퍼센트(참 5, atk 무관). 0이면 배수형.</param>
/// <param name="ExecuteCapPercent">처형 상한(참 10).</param>
/// <param name="BuildingOnly">건물 대상에만 배수 적용(분쇄). 유닛이면 평타.</param>
/// <param name="DamageReductionPercent">방어형 피해 감소(철벽 30).</param>
/// <param name="HealPercent">회복형 최대 병력 대비 회복(정비 15).</param>
/// <param name="HealCapPercent">회복 상한(기본 40).</param>
public sealed record ActiveSkill(
    string Code,
    string Name,
    ActiveType Type,
    string Grade,
    int DamageMultPercent = 100,
    int DefenderDfReductionPercent = 0,
    int ExecutePercent = 0,
    int ExecuteCapPercent = 0,
    bool BuildingOnly = false,
    int DamageReductionPercent = 0,
    int HealPercent = 0,
    int HealCapPercent = 40);
