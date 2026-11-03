using Godot;
using SanguoSLG.Core.Data;

namespace SanguoSLG.Game;

public sealed partial class ScenarioStartMenu
{
    private Control? _loadView;

    private void OpenLoadSlots(Control menuCenter)
    {
        if (_loadView is not null) return;
        menuCenter.Visible = false;
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);
        _loadView = center;
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(1060, 0) };
        var frame = new StyleBoxFlat { BgColor = new Color("1e2524"), BorderColor = new Color("d1ae65") };
        frame.SetBorderWidthAll(2);
        frame.SetCornerRadiusAll(12);
        frame.SetContentMarginAll(20);
        panel.AddThemeStyleboxOverride("panel", frame);
        center.AddChild(panel);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 10);
        panel.AddChild(content);
        var header = new HBoxContainer();
        content.AddChild(header);
        var title = new Label { Text = "저장된 게임 시작하기", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddThemeFontSizeOverride("font_size", 23);
        header.AddChild(title);
        var close = new Button { Text = "✕" };
        close.Pressed += () => { center.QueueFree(); _loadView = null; menuCenter.Visible = true; };
        header.AddChild(close);
        content.AddChild(new HSeparator());
        var grid = new GridContainer { Columns = 4 };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 8);
        content.AddChild(grid);
        var store = new SaveSlotStore(ProjectSettings.GlobalizePath("user://saves"),
            ProjectSettings.GlobalizePath("user://sanguo-save.json"));
        foreach (var info in store.InspectAll())
        {
            var label = !info.Exists ? "빈 슬롯" : info.Corrupt ? "손상된 저장" :
                $"{info.ScenarioTitle ?? $"{info.StartYear}년 시나리오"} · {info.FactionName ?? "세력 미상"}\n"
                + $"{info.Year}년 {info.Month}월 · {info.SavedAtUtc?.ToLocalTime():MM-dd HH:mm}";
            var button = new Button
            {
                Text = $"{info.Slot:00}  {label}",
                CustomMinimumSize = new Vector2(244, 62),
                Disabled = !info.Exists || info.Corrupt,
                Alignment = HorizontalAlignment.Left,
            };
            if (info.Corrupt) button.TooltipText = info.Error ?? "저장 파일을 읽을 수 없습니다.";
            var slot = info.Slot;
            button.Pressed += () => StartSavedGame(store, slot, content);
            grid.AddChild(button);
        }
    }

    private void StartSavedGame(SaveSlotStore store, int slot, VBoxContainer content)
    {
        try
        {
            var loaded = store.Load(slot);
            var data = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "data"));
            var source = CreatedGeneralPresets.ApplyAtNewGame(new ScenarioLoader().LoadFromDirectory(data));
            CampaignMapScene.ConfigureLoadedState(loaded, source);
            CampaignStartedFromMenu = true;
            _launchRequested = false;
            LoadSlotsOnStart = false;
            GetTree().ReloadCurrentScene();
        }
        catch (Exception error)
        {
            var warning = content.GetChildren().OfType<Label>().FirstOrDefault(label => label.Name == "LoadWarning");
            if (warning is null)
            {
                warning = new Label { Name = "LoadWarning", Modulate = new Color("f0a486") };
                content.AddChild(warning);
            }
            warning.Text = $"슬롯 {slot:00}을 불러올 수 없습니다: {error.Message}";
        }
    }
}
