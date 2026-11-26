namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public sealed record MerchantArtifact(string Code, string Name, string Effect, int Value,
    int GoldCost, int DefensePercent = 0)
{
    public EventTreasureDefinition TreasureDefinition =>
        new(Code, Name, "merchant", Effect, Value);
}

public static class MerchantCatalog
{
    public static readonly IReadOnlyList<MerchantArtifact> Artifacts =
    [
        new("green_dragon_blade", "청룡언월도", "might_percent", 5, 2000),
        new("sky_piercing_halberd", "방천화극", "might_percent", 10, 3500),
        new("serpent_spear", "장팔사모", "might_percent", 5, 2000),
        new("heavenly_sword", "의천검", "might_percent", 5, 2000),
        new("blue_steel_sword", "청강검", "might_percent", 5, 2000),
        new("paired_swords", "자웅일대검", "might_percent", 3, 1000),
        new("ancient_broadsword", "고정도", "might_percent", 3, 1000),
        new("meteor_hammer", "유성추", "might_percent", 3, 1000),
        new("three_point_blade", "삼첨도", "might_percent", 3, 1000),
        new("water_halberd", "수극", "might_percent", 3, 1000),
        new("red_hare", "적토마", "movement_percent", 10, 3000),
        new("yellow_lightning", "조황비전", "movement_percent", 10, 3000),
        new("dayuan_horse", "대완마", "movement_percent", 10, 3000),
        new("art_of_war", "손자병법", "intellect_percent", 2, 7000, 10),
        new("mengde_book", "맹덕신서", "intellect_percent", 5, 2000),
        new("taiping_magic", "태평요술서", "intellect_percent", 5, 2000),
        new("qingnang_book", "청낭서", "intellect_percent", 5, 2000),
        new("dunjia_book", "둔갑천서", "intellect_percent", 5, 2000),
        new("three_strategies", "삼략", "intellect_percent", 5, 2000),
        new("analects", "논어", "intellect_percent", 5, 2000),
    ];

    public static MerchantArtifact? Find(string code)
        => Artifacts.FirstOrDefault(artifact => artifact.Code == code);
}
