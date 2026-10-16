namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public sealed record GeneralStoryChoiceResult(GameState State, bool Ok, string Message);

/// <summary>대화 선택의 작은 보상을 1회만 원자적으로 확정한다. 화면 이동은 Game 계층이 맡는다.</summary>
public static class GeneralStoryChoices
{
    public static GeneralStoryChoiceResult Apply(GameState state, string eventId, string choice,
        BalanceConfig? balance = null)
    {
        var story = state.StoryPending.FirstOrDefault(e => e.Id == eventId);
        if (story is null) return new(state, false, "이미 처리했거나 존재하지 않는 대화입니다.");
        if (choice is not ("a" or "b")) return new(state, false, "선택지를 확인해 주세요.");
        if (choice == "b" && story.Kind != "victory")
            return new(GeneralStorySelector.Resolve(state, eventId, choice), true, "보고를 마쳤습니다.");

        GameState changed = state;
        string message;
        switch (story.Kind)
        {
            case "victory":
            {
                if (story.SecondSpeaker is null) return new(state, false, "함께한 부관이 없습니다.");
                var selected = choice == "a" ? story.Speaker : story.SecondSpeaker.Value;
                var index = state.Generals.ToList().FindIndex(g => g.Id == selected);
                if (index < 0) return new(state, false, "선봉 장수를 찾을 수 없습니다.");
                var generals = state.Generals.ToList();
                generals[index] = GeneralGrowth.AddGeneralExperience(generals[index], 20, out _);
                changed = state with { Generals = generals };
                message = $"{generals[index].Name} 전투 경험치 +20";
                break;
            }
            case "wounded":
            {
                if (!int.TryParse(story.Code, out var unitId)) return new(state, false, "부대가 없습니다.");
                var unit = state.Armies.FirstOrDefault(u => u.Id.Value == unitId && u.Field.Owner == story.Faction);
                if (unit is null || unit.Provisions < 50 || unit.Pool.Wounded <= 0)
                    return new(state, false, "부대·부상병 또는 휴대 군량 50이 부족합니다.");
                var recovered = System.Math.Min(25, unit.Pool.Wounded);
                if (unit.MaxTroops > 0) recovered = System.Math.Min(recovered, unit.MaxTroops - unit.Pool.Active);
                if (recovered <= 0) return new(state, false, "병력 한도에 도달했습니다.");
                changed = state with { FieldArmies = state.Armies.Select(u => u.Id == unit.Id
                    ? u with { Pool = u.Pool.Heal(recovered), Provisions = u.Provisions - 50 } : u).ToArray() };
                message = $"군량 -50, 부상병 {recovered}명 복귀";
                break;
            }
            case "wall":
            case "capture":
            case "security":
            {
                var city = state.Cities.FirstOrDefault(c => c.Id == story.City && c.Owner == story.Faction);
                var cost = story.Kind == "capture" ? 100 : 50;
                if (city is null || city.Gold < cost)
                    return new(state, false, $"해당 도시를 보유하고 금 {cost}이 있어야 합니다.");
                City updated;
                if (story.Kind == "wall")
                {
                    var maximum = CastleWall.Max(city.Castle, balance ?? new BalanceConfig(MonthlyTaxPerCity: 100), city.WallLevel);
                    if (city.Wall >= maximum) return new(state, false, "성벽 내구도가 이미 최대입니다.");
                    updated = city with { Gold = city.Gold - cost, Wall = System.Math.Min(maximum, city.Wall + 100) };
                    message = $"금 -50, 성벽 +{updated.Wall - city.Wall}";
                }
                else
                {
                    if (city.Security >= 100) return new(state, false, "치안이 이미 최대입니다.");
                    var increase = story.Kind == "capture" ? 2 : 1;
                    updated = city with { Gold = city.Gold - cost,
                        Security = System.Math.Min(100, city.Security + increase) };
                    message = $"금 -{cost}, 치안 +{updated.Security - city.Security}";
                }
                changed = state with { Cities = state.Cities.Select(c => c.Id == city.Id ? updated : c).ToArray() };
                break;
            }
            case "production":
            {
                var index = state.Generals.ToList().FindIndex(g => g.Id == story.Speaker);
                if (index < 0) return new(state, false, "수행 장수를 찾을 수 없습니다.");
                var generals = state.Generals.ToList();
                generals[index] = AdministrationGrowth.AddExperience(generals[index], 10, out _);
                changed = state with { Generals = generals };
                message = $"{generals[index].Name} 내정 경험치 +10";
                break;
            }
            default:
                message = story.Kind switch
                {
                    "beast_appeared" or "beast_defeated" => "단서·보물 보관함을 엽니다.",
                    "research" => "후속 연구 화면을 확인합니다.",
                    "low_food" => "식량이 없으면 부대가 탈주합니다.",
                    _ => "대화를 기록했습니다. 추가 자원은 지급하지 않습니다.",
                };
                break;
        }
        return new(GeneralStorySelector.Resolve(changed, eventId, choice), true, message);
    }
}
