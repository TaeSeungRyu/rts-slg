namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public enum GameDifficulty { Normal, Hard }

public static class GameDifficultyRules
{
    public static int Production(GameState state, FactionId owner, int amount)
        => state.Difficulty == GameDifficulty.Hard
            && owner.Value != state.HumanFactionId && owner.Value != 0
                ? (int)Math.Min(int.MaxValue, (long)amount * 120 / 100)
                : amount;
}
