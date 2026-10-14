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
                "7일 후부터 주변 숲·산지에 대호가 출현합니다. 숲·산지가 없으면 빈 평지에 출현하며, 다른 대호가 활동 중이면 기다립니다.",
                "대호 처치: 장수 경험치·병종 숙련·호부 획득. 용·봉황은 후속 이벤트입니다.",
                ExplorationRewardTone.DivineBeast, discovery.Gold, discovery.Provisions),
            "dragon_trace" => new(
                "용의 흔적", "희귀 이벤트 단서",
                $"{explorerName} 장수가 {cityName}에서 용의 흔적을 발견했습니다.",
                "7일 후부터 발견 도시 주변에 용이 출현합니다.",
                "용 처치: 장수 경험치·병종 숙련과 용린 갑옷·용각·용의 알 중 하나를 선택합니다.",
                ExplorationRewardTone.DivineBeast, discovery.Gold, discovery.Provisions),
            "phoenix_trace" => new(
                "봉황의 흔적", "희귀 이벤트 단서",
                $"{explorerName} 장수가 {cityName}에서 봉황의 흔적을 발견했습니다.",
                "7일 후부터 발견 도시 주변에 봉황이 출현합니다.",
                "봉황 처치: 장수 경험치·병종 숙련과 봉황 깃·봉황옥·봉황의 알 중 하나를 선택합니다.",
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
