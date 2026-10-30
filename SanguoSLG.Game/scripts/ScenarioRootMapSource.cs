using Godot;
using SanguoSLG.Core.Data;

namespace SanguoSLG.Game;

internal static class ScenarioRootMapSource
{
    public static string Path => ProjectSettings.GlobalizePath("user://map-presets/east-asia.json");

    public static ScenarioMapPreset Load()
    {
        if (!File.Exists(Path))
        {
            var bundled = ProjectSettings.GlobalizePath("res://assets/maps/east-asia-user-preset.json");
            ScenarioMapPresetStore.Save(Path, ScenarioMapPresetStore.Load(bundled));
            GD.Print($"[scenario-root-map] created={Path}");
        }
        return ScenarioMapPresetStore.Load(Path);
    }

    public static ScenarioMapVersion OpenVersion(ScenarioMapArchive archive)
        => archive.SaveVersion("default-east-asia", "기본 지도", Load());

    public static ScenarioMapPreset PreviousVersion(ScenarioMapArchive archive, ScenarioDraft draft)
    {
        try { return archive.Load(draft.MapId, draft.MapRevision).Preset; }
        catch (FileNotFoundException) when (draft.MapId == "default-east-asia")
        {
            var bundled = ScenarioMapPresetStore.Load(ProjectSettings.GlobalizePath(
                "res://assets/maps/east-asia-user-preset.json"));
            if (ScenarioMapArchive.RevisionOf(bundled) == draft.MapRevision) return bundled;
            throw;
        }
    }
}
