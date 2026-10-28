namespace SanguoSLG.Core.Data;

public sealed record MapRuinKind(string TroopCode, string Name, int Defenders, bool Naval = false);

public static class MapRuinCatalog
{
    public static readonly IReadOnlyList<MapRuinKind> All =
    [
        new("geukbyeong", "극병 유적", 15_000),
        new("war_elephant", "상병 유적", 50_000),
        new("namman", "남만병 유적", 20_000),
        new("deunggap", "등갑병 유적", 20_000),
        new("mudang", "무당비군 유적", 20_000),
        new("cataphract", "철기병 유적", 30_000),
        new("horse_archer", "궁기병 유적", 30_000),
        new("hwarang", "화랑궁병 유적", 30_000),
        new("turtleship", "거북선 유적", 50_000, true),
        new("waeseon", "왜선 유적", 5_000, true),
    ];

    public static MapRuinKind? Find(string troopCode)
        => All.FirstOrDefault(kind => kind.TroopCode == troopCode);
}
