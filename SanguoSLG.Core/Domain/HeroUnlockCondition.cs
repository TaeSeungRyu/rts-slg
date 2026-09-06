namespace SanguoSLG.Core.Domain;

/// <summary>위인 해금 조건 한 줄. 판정 로직은 조건 코드와 값을 해석한다.</summary>
public sealed record HeroUnlockCondition(
    string Code,
    int Value = 0,
    string? Text = null,
    string? TroopCode = null,
    FactionId? Faction = null,
    CityId? City = null,
    string? Region = null);
