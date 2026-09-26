namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public sealed record TroopProductionAccess(bool Allowed, string Reason)
{
    public static readonly TroopProductionAccess Granted = new(true, string.Empty);
}

/// <summary>모든 병력 생산 경로가 공유하는 세력별 생산권 판정.</summary>
public static class FactionTroopUnlock
{
    public const int SiegeResearchLevel = 5;
    private static readonly HashSet<string> Basic = new(StringComparer.Ordinal)
    { "swordsman", "archer", "cavalry", "thunder_cart", "small_boat", "medium_ship", "large_ship" };
    private static readonly Dictionary<string, string> RuinNames = new(StringComparer.Ordinal)
    {
        ["geukbyeong"] = "극병 유적", ["war_elephant"] = "상병 유적", ["namman"] = "남만병 유적",
        ["deunggap"] = "등갑병 유적", ["mudang"] = "무당비군 유적", ["cataphract"] = "철기병 유적",
        ["horse_archer"] = "궁기병 유적", ["hwarang"] = "화랑궁병 유적",
        ["turtleship"] = "거북선 유적", ["waeseon"] = "왜선 유적",
    };

    public static TroopProductionAccess Check(GameState state, FactionId faction, string troopCode)
    {
        if (Basic.Contains(troopCode)) return TroopProductionAccess.Granted;
        if (troopCode is "catapult" or "siege_tower")
        {
            var level = state.ResearchOf(faction, "thunder_cart");
            return level >= SiegeResearchLevel ? TroopProductionAccess.Granted
                : new(false, $"충차 연구 Lv.{SiegeResearchLevel} 필요 · 현재 Lv.{level}");
        }
        if (RuinNames.ContainsKey(troopCode)
            && state.RuinStatus.Any(r => r.Registrations.Contains(faction)
                && state.Ruins.Any(d => d.Id == r.RuinId && d.TroopCode == troopCode)))
            return TroopProductionAccess.Granted;
        return RuinNames.TryGetValue(troopCode, out var ruin)
            ? new(false, $"{ruin} 점령 필요") : new(false, "생산 해금 조건을 충족하지 못했다.");
    }

    public static string RuinName(string troopCode)
        => RuinNames.TryGetValue(troopCode, out var name) ? name : troopCode;
}
