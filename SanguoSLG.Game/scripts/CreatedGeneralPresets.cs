namespace SanguoSLG.Game;

using Godot;
using SanguoSLG.Core.Data;

public static class CreatedGeneralPresets
{
    public const string UserPath = "user://general-presets/created-generals.json";

    public static Scenario ApplyAtNewGame(Scenario scenario)
    {
        var path = ProjectSettings.GlobalizePath(UserPath);
        if (!File.Exists(path)) return scenario;
        try { return CreatedGeneralStore.Apply(scenario, CreatedGeneralStore.Load(File.ReadAllText(path))); }
        catch (Exception error)
        {
            GD.PushWarning($"생성 장수를 적용하지 못했습니다: {error.Message}");
            return scenario;
        }
    }
}
