namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>Phase 10 탐색 보상 판정. 인재, 도적/반란, 재난은 포함하지 않는다.</summary>
public sealed class ExplorationService
{
    public ExplorationDiscovery Explore(GameState state, City city, General explorer, IRandomSource random)
    {
        var roll = random.Next(0, 100);
        var kind = KindForRoll(roll, explorer.Politics);
        return BuildDiscovery(state.Day, city.Owner, city.Id, explorer.Id, kind);
    }

    public static ExplorationResultKind KindForRoll(int roll, int politics = 0)
    {
        var localClanBonus = System.Math.Clamp(politics, 0, 100) / 10;
        return KindForRollWithLocalClanBonus(roll, localClanBonus);
    }

    private static ExplorationResultKind KindForRollWithLocalClanBonus(int roll, int localClanBonus)
        => roll switch
        {
            < 1 => ExplorationResultKind.DivineBeast,
            < 2 => ExplorationResultKind.AncientRelic,
            _ when roll < 12 + localClanBonus => ExplorationResultKind.LocalClan,
            _ when roll < 17 + localClanBonus => ExplorationResultKind.Rumor,
            _ => ExplorationResultKind.None,
        };

    private static ExplorationDiscovery BuildDiscovery(
        int day, FactionId faction, CityId city, GeneralId explorer, ExplorationResultKind kind)
        => kind switch
        {
            ExplorationResultKind.DivineBeast => new(day, faction, city, explorer, kind,
                "divine_beast_trace", Text: "신수의 흔적을 발견했습니다."),
            ExplorationResultKind.AncientRelic => new(day, faction, city, explorer, kind,
                "ancient_relic_clue", Text: "고대유물의 단서를 발견했습니다."),
            ExplorationResultKind.LocalClan => new(day, faction, city, explorer, kind,
                "local_clan_support", Gold: 200, Provisions: 600, Text: "지방호족이 금과 군량을 지원했습니다."),
            ExplorationResultKind.Rumor => new(day, faction, city, explorer, kind,
                "rumor_clue", Text: "의미심장한 소문을 들었습니다."),
            _ => new(day, faction, city, explorer, ExplorationResultKind.None,
                "nothing", Text: "별다른 성과가 없었습니다."),
        };
}
