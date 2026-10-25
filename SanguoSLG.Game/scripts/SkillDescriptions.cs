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
                if (skill.StrikeAtkBonusPercent > 0)
                    effects.Add($"이번 공격에 한해 공격 보정 +{skill.StrikeAtkBonusPercent}%를 적용합니다. 단일 주대상에게만 액티브 피해를 줍니다.");
                if (skill.CavalryDamageMultPercent > 0)
                    effects.Add($"기병 분류로 발동하면 피해가 {skill.CavalryDamageMultPercent}%로 증가합니다.");
                if (skill.BuildingOnly) { effects.Add("위 피해 배수는 건물 대상에 적용됩니다. 부대 대상에는 기본 공격 배수를 사용합니다."); }
                if (skill.DefenderDfReductionPercent > 0)
                {
                    effects.Add($"이번 타격은 대상 방어력을 {skill.DefenderDfReductionPercent}% 낮춰 계산합니다.");
                }
                if (skill.Code == "armor_break")
                    effects.Add("공격 후 대상의 방어력 −20%가 3진행 동안 유지됩니다.");
                if (skill.Code == "tiger_strike")
                    effects.Add("이번 교전에서 대상의 반격 피해를 50% 줄입니다.");
                if (skill.Code == "chain_strike")
                    effects.Add("두 번 베어 피해를 나눕니다. 대상이 둘 이상이면 앞의 두 대상에게 나눠 적용합니다.");
                if (skill.Code == "double_hit")
                    effects.Add("한 대상에게 두 차례 타격으로 나눠 피해를 줍니다.");
                if (skill.Code == "barrage")
                    effects.Add("궁병·공성 병종으로 발동하면 일반 공격 피해의 180%를 적용합니다.");
                effects.Add("표시 수치는 무력 60 기준이며 발동에 적용되는 장수의 무력에 따라 위력이 달라집니다.");
                break;
            case ActiveType.Defense:
                if (skill.Code == "hold_the_line")
                {
                    effects.Add("수성형 · 성·항구에 주둔 중이면 성벽이 받는 피해를 50% 줄입니다.");
                }
                else if (skill.Code == "evasion")
                {
                    effects.Add("방어형 · 일반 공격을 유지하며 발동한 진행부터 3진행 동안 원거리(사거리 2 이상) 피해를 30% 줄입니다.");
                    effects.Add("근접 피해에는 적용되지 않으며, 감소율은 무력과 관계없이 30%로 고정됩니다.");
                }
                else
                {
                    effects.Add($"방어형 · 일반 공격을 유지하며 이번 교전에서 받는 피해를 {skill.DamageReductionPercent}% 줄입니다.");
                    effects.Add("무력 60 기준이며 선봉 무력에 따라 달라집니다. 피해 감소 상한은 75%입니다.");
                    if (skill.Code == "riposte")
                        effects.Add("실제로 받은 피해의 15%를 공격자에게 반사합니다.");
                    if (skill.Code == "turtle_formation")
                        effects.Add("두 부대 이상에게 포위당하면 피해 감소율이 두 배가 됩니다.");
                }
                break;
            case ActiveType.Heal:
                if (skill.Code == "rally")
                {
                    effects.Add("지원형 · 일반 공격을 유지합니다.");
                    effects.Add("발동한 진행부터 3진행 동안 공격·방어 보정이 각각 +10% 적용됩니다. 병력 회복 스킬은 아닙니다.");
                }
                else
                {
                    effects.Add("회복형 · 일반 공격을 유지합니다.");
                    effects.Add($"최대 병력의 {skill.HealPercent}%를 부상병에서 즉시 회복합니다. 부상병 수를 초과해 회복하지 않습니다.");
                    effects.Add($"지력 60 기준이며 선봉 지력에 따라 달라집니다. 회복률 상한은 {skill.HealCapPercent}%입니다.");
                    if (skill.Code == "resupply")
                        effects.Add($"이후 3진행 동안 매 진행 같은 양을 부상병에서 회복합니다. 발동 시 휴대 군량을 최대 적재량의 {skill.ProvisionPercent}%만큼 보충합니다(적재 상한 적용).");
                    if (skill.Code == "second_wind")
                        effects.Add("발동한 진행부터 3진행 동안 방어 보정 +10%를 적용합니다.");
                }
                break;
            case ActiveType.Tactic:
                effects.Add("책략형 · 교전 중 대상에게 특수 효과를 적용합니다.");
                if (!string.IsNullOrWhiteSpace(skill.Description))
                {
                    effects.Add(skill.Description);
                }
                break;
        }
        effects.Add(skill.Code == "hold_the_line"
            ? "성·항구에서 수성 준비 5일을 채운 뒤 적용됩니다. 야전 스킬원을 소비하지 않습니다."
            : $"실제 교전 {ActiveGauge.ReadyDays}회로 스킬원을 채우고 다음 유효한 교전에서 자동 발동합니다. 이동만 한 진행은 충전되지 않습니다. 한 교전에는 부대당 1개만 발동하며 선봉·부관이 번갈아 사용합니다. 발동하거나 성으로 복귀하면 충전이 초기화됩니다.");
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
            "security" => $"태수로 재임 중인 도시의 월말 치안 회복 +{amount / 10}. 7일 치안담당 업무에는 적용되지 않습니다.",
            "ore_output" => $"광석을 산출하는 도시에서 태수 재임 시 광석 산출 +{amount}%.",
            "horse_output" => $"말을 산출하는 도시에서 태수 재임 시 말 산출 +{amount}%.",
            "recruit_amount" => $"병력담당으로 배치되면 모든 병종의 병력 생산량 +{amount}%.",
            "cavalry_output" => $"병력담당으로 배치되면 기병 계열 생산량 +{amount}%.",
            "elephant_output" => $"병력담당으로 배치되면 상병 계열 생산량 +{amount}%.",
            "training" => $"훈련 명령 수행 시 훈련도 증가량 +{amount}. 7일 훈련담당 업무에는 적용되지 않습니다.",
            "recruit_cost" => $"모집 시 인구 감소량 -{amount}%.",
            "market_discount" => $"시장 구매가 -{amount}%.",
            "provisions" => $"선봉·부관으로 편성 시 군량 소모 -{amount}%. 보급부대에는 적용되지 않습니다.",
            "wall" => $"태수 재임 시 성벽 수리 회복 비율 +{amount}%p.",
            _ => "효과 정보가 없습니다.",
        };
        var requirement = skill.Bucket is "tax" or "harvest" or "ore_output" or "horse_output"
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
