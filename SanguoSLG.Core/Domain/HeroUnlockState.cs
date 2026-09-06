namespace SanguoSLG.Core.Domain;

/// <summary>특정 위인 정의의 현재 해금/영입 상태.</summary>
public sealed record HeroUnlockState(
    GeneralId General,
    HeroUnlockStatus Status,
    FactionId? EligibleFaction = null,
    int UpdatedDay = 1)
{
    public bool CanRecruit => Status is HeroUnlockStatus.Unlocked or HeroUnlockStatus.Wanderer;
}
