namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 전투 패시브 스킬 정의(design-skill-passives.md). 하나의 스킬이 여러 조건부 효과를 가질 수 있다
/// (예: 광전사 = 공+·방-, 배수진 = 병력 50%↓ +·초과 -). 수치는 data/passive-skills.json에서 온다.
/// </summary>
/// <param name="Code">스킬 코드.</param>
/// <param name="Name">한국어 이름.</param>
/// <param name="Grade">등급("high"=상위 무장, "low"=하위 무장).</param>
/// <param name="Effects">조건부 효과 목록.</param>
public sealed record PassiveSkill(
    string Code,
    string Name,
    string Grade,
    IReadOnlyList<PassiveEffect> Effects);
