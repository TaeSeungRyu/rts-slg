namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>군단 위임 진행에서는 자동 장수 대화를 기록으로 넘기고 화면 확인을 요구하지 않는다.</summary>
public static class ArmyCorpsStoryPolicy
{
    public static GameState SuppressAutomaticDialog(GameState state, FactionId faction)
    {
        if (!state.Corps.Any(corps => corps.Faction == faction)) return state;
        foreach (var story in state.StoryPending.Where(story => story.Faction == faction).ToArray())
            state = GeneralStorySelector.Resolve(state, story.Id, "b");
        return state;
    }
}
