namespace SanguoSLG.Core.Domain;

/// <summary>시나리오가 제공하는 위인 해금 정적 정의.</summary>
public sealed record HeroUnlockDefinition(
    GeneralId General,
    HeroUnlockType Type,
    FactionId? Faction = null,
    IReadOnlyList<string>? HomeRegions = null,
    IReadOnlyList<CityId>? HomeCities = null,
    IReadOnlyList<HeroUnlockCondition>? Conditions = null,
    IReadOnlyList<HeroUnlockCondition>? WandererConditions = null,
    int RecruitGold = 0,
    bool AiCanRecruit = true,
    string Title = "",
    string Desc = "")
{
    public IReadOnlyList<string> RegionList => HomeRegions ?? [];

    public IReadOnlyList<CityId> CityList => HomeCities ?? [];

    public IReadOnlyList<HeroUnlockCondition> ConditionList => Conditions ?? [];

    public IReadOnlyList<HeroUnlockCondition> WandererConditionList => WandererConditions ?? [];
}
