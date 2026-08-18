namespace SanguoSLG.Core.AI;

/// <summary>
/// 세력 AI 행동 문턱값(design-plan 12단계 "세력 AI 최소"). 게임 밸런스가 아니라 AI 성향 튜닝이라
/// 코드 기본값으로 두되 주입 가능하게 한다. 결정론: 값 기반 판단만, 난수 없음.
/// </summary>
/// <param name="Troop">모집·출전에 쓰는 기본 병종.</param>
/// <param name="DeployTarget">대기 병력이 이 이상이면 출전을 고려한다.</param>
/// <param name="DeploySize">한 번에 편성하는 병력(일반 부대 상한 이하).</param>
/// <param name="MinOre">광석이 이 이상일 때만 모집한다.</param>
/// <param name="KeepGeneralsHome">출전하려면 도시에 남는 자유 장수가 이 수 이상이어야 한다(모집용 확보).</param>
public sealed record AiConfig(
    string Troop = "swordsman",
    int DeployTarget = 8000,
    int DeploySize = 10000,
    int MinOre = 300,
    int KeepGeneralsHome = 1);
