using Godot;
using SanguoSLG.Core.Data;

namespace SanguoSLG.Game;

public sealed partial class EditMapScene
{
    private Control? _mapLibrary;

    private static string RootMapPath()
        => ProjectSettings.GlobalizePath("user://map-presets/east-asia.json");

    private static string CustomMapDirectory()
        => ProjectSettings.GlobalizePath("user://map-presets/custom");

    private static string MapIdFor(string path)
        => path == RootMapPath() ? "legacy-east-asia" : Path.GetFileNameWithoutExtension(path);

    private void ShowMapLibrary()
    {
        _mapLibrary?.QueueFree();
        var overlay = new ColorRect { Color = new Color(0, 0, 0, 0.82f), MouseFilter = MouseFilterEnum.Stop };
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(overlay);
        _mapLibrary = overlay;
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        overlay.AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(680, 460) };
        panel.AddThemeStyleboxOverride("panel", PanelStyle());
        center.AddChild(panel);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 10);
        panel.AddChild(content);
        var header = new HBoxContainer();
        header.AddChild(new Label { Text = "지도 목록", SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var close = new Button { Text = "닫기" };
        close.Pressed += CloseMapLibrary;
        header.AddChild(close);
        content.AddChild(header);
        content.AddChild(new Label { Text = "root는 원본 편집 지도입니다. 복제하거나 새 지도를 추가해 별도로 관리할 수 있습니다." });
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        scroll.AddChild(list);
        content.AddChild(scroll);
        var paths = new[] { RootMapPath() }
            .Concat(Directory.Exists(CustomMapDirectory())
                ? Directory.EnumerateFiles(CustomMapDirectory(), "*.json").OrderBy(path => path)
                : []);
        foreach (var path in paths)
        {
            if (!File.Exists(path)) continue;
            ScenarioMapPreset preset;
            try { preset = ScenarioMapPresetStore.Load(path); }
            catch (Exception error)
            {
                list.AddChild(new Label { Text = Path.GetFileName(path) + ": " + error.Message });
                continue;
            }
            var row = new HBoxContainer();
            list.AddChild(row);
            var title = path == RootMapPath() ? "root · 원본 지도"
                : MapArchive().SaveVersion(MapIdFor(path), Path.GetFileNameWithoutExtension(path), preset).Title;
            row.AddChild(new Label
            {
                Text = title,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                TooltipText = $"{preset.Width}×{preset.Height} · 거점 {preset.Sites.Count(site => site.Kind is "city" or "port" or "gate")}\n{path}",
            });
            var edit = new Button { Text = "수정" };
            edit.Pressed += () => LoadSelectedMap(path, preset);
            row.AddChild(edit);
            var clone = new Button { Text = "복제" };
            clone.Pressed += () => CreateMapCopy(preset);
            row.AddChild(clone);
            var remove = new Button { Text = "삭제", Disabled = path == RootMapPath() };
            remove.Pressed += () => ConfirmDeleteMap(path);
            row.AddChild(remove);
        }
        var add = new Button { Text = "새 지도 추가 · root에서 시작" };
        add.Pressed += () => CreateMapCopy(ScenarioMapPresetStore.Load(RootMapPath()));
        content.AddChild(add);
    }

    private void CloseMapLibrary()
    {
        _mapLibrary?.QueueFree();
        _mapLibrary = null;
    }

    private void LoadSelectedMap(string path, ScenarioMapPreset preset, string? title = null)
    {
        var version = MapArchive().SaveVersion(MapIdFor(path),
            title ?? (path == RootMapPath() ? "root 지도" : Path.GetFileNameWithoutExtension(path)), preset);
        _selectedMapPath = path;
        _activeMapVersion = version;
        _width = preset.Width;
        _height = preset.Height;
        _draftUndo.Clear();
        _draftRedo.Clear();
        RestoreDraft(preset);
        _draftChanged = false;
        if (_saveDraftButton is not null) _saveDraftButton.Text = "맵 저장";
        CloseMapLibrary();
        CallDeferred(nameof(FitMap));
    }

    private void CreateMapCopy(ScenarioMapPreset preset)
    {
        var id = "user-" + Guid.NewGuid().ToString("N");
        var path = Path.Combine(CustomMapDirectory(), id + ".json");
        var number = Directory.Exists(CustomMapDirectory())
            ? Directory.EnumerateFiles(CustomMapDirectory(), "*.json").Count() + 1 : 1;
        ScenarioMapPresetStore.Save(path, preset);
        LoadSelectedMap(path, preset, "사용자 지도 " + number);
        ShowSaveResult("지도 생성 완료", $"새 지도 파일을 만들었습니다.\n{path}");
    }

    private void ConfirmDeleteMap(string path)
    {
        if (path == RootMapPath()) return;
        var references = MapScenarioReferences(MapIdFor(path));
        if (references.Count > 0)
        {
            ShowSaveResult("지도 삭제 불가", "시나리오에서 사용 중입니다: " + string.Join(", ", references));
            return;
        }
        var dialog = new ConfirmationDialog
        {
            Title = "지도 삭제",
            DialogText = $"{Path.GetFileNameWithoutExtension(path)} 지도를 삭제하시겠습니까?",
            OkButtonText = "삭제",
        };
        AddChild(dialog);
        dialog.Confirmed += () =>
        {
            if (MapScenarioReferences(MapIdFor(path)).Count > 0) return;
            File.Delete(path);
            if (_selectedMapPath == path)
                LoadSelectedMap(RootMapPath(), ScenarioMapPresetStore.Load(RootMapPath()));
            else ShowMapLibrary();
        };
        dialog.Confirmed += dialog.QueueFree;
        dialog.Canceled += dialog.QueueFree;
        dialog.PopupCentered();
    }
}
