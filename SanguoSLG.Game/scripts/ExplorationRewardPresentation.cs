using SanguoSLG.Core.Domain;

namespace SanguoSLG.Game;

public enum ExplorationRewardTone
{
    Empty,
    Resource,
    Rumor,
    Relic,
    DivineBeast,
}

/// <summary>Core 탐색 이력을 보상 모달·단서 보관함에서 함께 쓰는 표시 데이터로 변환한다.</summary>
public sealed record ExplorationRewardPresentation(
    string Title,
    string Badge,
    string Summary,
    string Detail,
    string RegistrationCondition,
    ExplorationRewardTone Tone,
    int Gold,
    int Provisions)
{
    public bool IsClue => Tone is ExplorationRewardTone.Rumor or ExplorationRewardTone.Relic or ExplorationRewardTone.DivineBeast;
    public bool HasResources => Gold > 0 || Provisions > 0;

    public static ExplorationRewardPresentation From(
        ExplorationDiscovery discovery, string cityName, string explorerName)
        => discovery.Code switch
        {
            "divine_beast_trace" => new(
                "신수의 흔적", "희귀 이벤트 단서",
                $"{explorerName} 장수가 {cityName}에서 신수의 흔적을 발견했습니다.",
                "용·봉황 등 신수 이벤트로 이어지는 희귀 단서입니다.",
                "등록 조건: 후속 신수 이벤트를 발견하고 전투 조건을 달성해야 합니다.",
                ExplorationRewardTone.DivineBeast, discovery.Gold, discovery.Provisions),
            "ancient_relic_clue" => new(
                "고대유물의 단서", "아이템 단서",
                $"{explorerName} 장수가 {cityName}에서 고대유물의 단서를 발견했습니다.",
                "병법서·고대 무구 등 희귀 아이템 이벤트로 이어지는 단서입니다.",
                "등록 조건: 후속 유물 이벤트를 완료하면 보물로 등록됩니다.",
                ExplorationRewardTone.Relic, discovery.Gold, discovery.Provisions),
            "local_clan_support" => new(
                "지방호족의 지원", "자원 보상",
                $"{cityName}의 지방호족이 세력에 물자를 지원했습니다.",
                "지원받은 금과 군량은 해당 도시에 즉시 반영됩니다.",
                "즉시 획득",
                ExplorationRewardTone.Resource, discovery.Gold, discovery.Provisions),
            "rumor_clue" => new(
                "소문과 단서", "지역 단서",
                $"{explorerName} 장수가 {cityName}에서 의미심장한 소문을 들었습니다.",
                "관련 지역의 위인·신수·유물 이벤트를 여는 후속 단서 후보입니다.",
                "등록 조건: 관련 지역에서 후속 탐색 또는 이벤트 조건을 달성해야 합니다.",
                ExplorationRewardTone.Rumor, discovery.Gold, discovery.Provisions),
            _ => new(
                "탐색 완료", "성과 없음",
                $"{explorerName} 장수가 {cityName} 탐색을 마쳤습니다.",
                "이번 탐색에서는 특별한 성과를 발견하지 못했습니다.",
                "다음 탐색을 진행할 수 있습니다.",
                ExplorationRewardTone.Empty, discovery.Gold, discovery.Provisions),
        };
}
