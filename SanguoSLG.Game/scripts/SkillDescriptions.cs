namespace SanguoSLG.Game;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

internal static class SkillDescriptions
{
    public static string Active(ActiveSkill? skill)
    {
        if (skill is null) { return "스킬 정보를 찾을 수 없습니다."; }
        var effects = new List<string>();
        switch (skill.Type)
        {
            case ActiveType.Strike:
                effects.Add("타격형 · 일반 공격을 대체합니다.");
                if (skill.ExecutePercent > 0)
                {
                    effects.Add($"대상 현재 병력의 {skill.ExecutePercent}%만큼 피해를 줍니다. (상한 {skill.ExecuteCapPercent}%)");
                }
                else
                {
                    effects.Add($"일반 공격 피해의 {skill.DamageMultPercent}%로 공격합니다.");
                }
                if (skill.BuildingOnly) { effects.Add("위 피해 배수는 건물 대상에 적용됩니다. 부대 대상에는 기본 공격 배수를 사용합니다."); }
                if (skill.DefenderDfReductionPercent > 0)
                {
                    effects.Add($"이번 타격은 대상 방어력을 {skill.DefenderDfReductionPercent}% 낮춰 계산합니다.");
                }
                effects.Add("표시 수치는 무력 60 기준이며 부대 선봉의 무력에 따라 위력이 달라집니다.");
                break;
            case ActiveType.Defense:
                effects.Add($"방어형 · 일반 공격을 유지하며 이번 교전에서 받는 피해를 {skill.DamageReductionPercent}% 줄입니다.");
                effects.Add("무력 60 기준이며 선봉 무력에 따라 달라집니다. 피해 감소 상한은 75%입니다.");
                break;
            case ActiveType.Heal:
                effects.Add("회복형 · 일반 공격을 유지합니다.");
                effects.Add(skill.HealPercent > 0
                    ? $"최대 병력의 {skill.HealPercent}%를 부상병에서 회복합니다. 부상병 수를 초과해 회복하지 않습니다."
                    : "현재 적용되는 병력 회복 효과는 없습니다. 추가 효과는 구현 예정입니다.");
                effects.Add($"지력 60 기준이며 선봉 지력에 따라 달라집니다. 회복률 상한은 {skill.HealCapPercent}%입니다.");
                break;
            case ActiveType.Tactic:
                effects.Add("책략형 · 기존 계략을 전투 액티브 슬롯으로 전환한 스킬입니다.");
                if (!string.IsNullOrWhiteSpace(skill.Description))
                {
                    effects.Add(skill.Description);
                }
                effects.Add("실제 전투 효과 배선은 Phase 14A에서 진행합니다. 지금은 에디터 선택과 데이터 검증에 등록된 상태입니다.");
                break;
        }
        effects.Add($"야전 {ActiveGauge.ReadyDays}일 충전 후 유효한 교전에서 자동 발동합니다. 부대당 교전 1회 발동하며 선봉이 우선합니다. 사용하거나 성으로 복귀하면 충전이 초기화됩니다.");
        if (skill.Code is "armor_break" or "tiger_strike" or "chain_strike" or "breakthrough"
            or "one_man_army" or "barrage" or "riposte" or "turtle_formation" or "evasion"
            or "hold_the_line" or "resupply" or "second_wind")
        {
            effects.Add("현재 적용되는 기본 효과입니다. 추가 지속 효과·조건부 강화 등은 후속 스킬 확장 단계에서 반영됩니다.");
        }
        return string.Join("\n\n", effects);
    }

    public static string Passive(PassiveSkill? skill, int tier)
    {
        if (skill is null) { return "스킬 정보를 찾을 수 없습니다."; }
        var lines = skill.Effects.Select(effect =>
            $"{Condition(effect.Condition)}: {(effect.Bucket == SkillBucket.Attack ? "공격" : "방어")} 보정 {effect.AmountAtTier(tier):+0;-0;0}%");
        return $"전투 패시브 · Lv{tier}\n조건을 만족하면 자동 적용됩니다.\n\n{string.Join("\n", lines)}\n\n선봉과 부관의 패시브가 함께 반영됩니다.";
    }

    public static string Admin(AdminSkill? skill, int tier)
    {
        if (skill is null) { return "스킬 정보를 찾을 수 없습니다."; }
        var amount = skill.AmountAtTier(tier);
        var effect = skill.Bucket switch
        {
            "tax" => $"태수의 도시 기본 금 수입 +{amount}%. 내정 담당자의 별도 자동 증가량에는 적용되지 않습니다.",
            "harvest" => $"태수의 도시 기본 군량 수입 +{amount}%. 내정 담당자의 별도 자동 증가량에는 적용되지 않습니다.",
            "security" => $"기존 태수 치안 회복 +{amount / 10}. 현재 자동 담당자 치안 계산에는 연결되지 않은 효과입니다.",
            "ore_output" => $"광석을 산출하는 도시에서 태수 재임 시 광석 산출 +{amount}%.",
            "horse_output" => $"말을 산출하는 도시에서 태수 재임 시 말 산출 +{amount}%.",
            "elephant_output" => $"코끼리를 산출하는 도시에서 태수 재임 시 코끼리 산출 +{amount}%.",
            "recruit_amount" => $"기존 모집 명령 병력 +{amount}%. 현재 자동 병력 생산에는 연결되지 않은 효과입니다.",
            "training" => $"기존 훈련 명령 상승량 +{amount}. 현재 훈련 담당자의 자동 훈련에는 연결되지 않은 효과입니다.",
            "recruit_cost" => $"기존 모집 시 인구 감소량 -{amount}%. 인구·모집 명령 제거로 현재 사용하지 않습니다.",
            "market_discount" => $"기존 시장 구매가 -{amount}%. 시장 제거로 현재 사용하지 않습니다.",
            "provisions" => $"선봉·부관으로 편성 시 군량 소모 -{amount}%. 보급부대에는 적용되지 않습니다.",
            "wall" => $"태수 재임 시 성벽 수리 회복 비율 +{amount}%p.",
            _ => "효과 설명이 아직 등록되지 않았습니다.",
        };
        var requirement = skill.Bucket is "tax" or "harvest" or "ore_output" or "horse_output" or "elephant_output"
            ? "\n\n해당 도시에 재임 중인 태수가 경제 활동의 정치 조건을 만족해야 적용됩니다." : "";
        return $"내정 패시브 · Lv{tier}\n직접 사용하는 액티브가 아닙니다.\n\n{effect}{requirement}";
    }

    private static string Condition(PassiveCondition condition) => condition switch
    {
        PassiveCondition.Always => "항상",
        PassiveCondition.TargetBuilding => "성·항구 공격 시",
        PassiveCondition.TargetUnit => "부대 공격 시",
        PassiveCondition.Rough => "숲·산 지형에서",
        PassiveCondition.PlainsDesert => "평야·사막에서",
        PassiveCondition.Momentum => "여러 아군이 적을 포위할 때",
        PassiveCondition.Pursuit => "추격 중",
        PassiveCondition.EnemyMarching => "행군 중인 적에게",
        PassiveCondition.Melee => "근접 공격 시",
        PassiveCondition.MeleeIncoming => "근접 공격을 받을 때",
        PassiveCondition.RangedIncoming => "원거리 공격을 받을 때",
        PassiveCondition.HpBelowHalf => "병력 50% 이하",
        PassiveCondition.HpAboveHalf => "병력 50% 초과",
        PassiveCondition.CastleGarrison => "성·항구 주둔 중",
        PassiveCondition.Surrounded => "적에게 포위당했을 때",
        PassiveCondition.Field => "성·항구 밖 야전에서",
        _ => "조건 정보 없음",
    };
}
