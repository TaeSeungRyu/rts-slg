using Godot;

namespace SanguoSLG.Game;

public sealed partial class ScenarioStartMenu : Control
{
    private static bool _launchRequested;
    public static string? SelectedPresetPath { get; private set; }
    public static bool FromStartMenu { get; private set; }

    public static bool ConsumeLaunchRequest()
    {
        if (!_launchRequested) return false;
        _launchRequested = false;
        return true;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        var background = new ColorRect { Color = new Color("172c2d") };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(460, 350) };
        var style = new StyleBoxFlat { BgColor = new Color("1e2524"), BorderColor = new Color("d1ae65") };
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(12);
        style.SetContentMarginAll(25);
        panel.AddThemeStyleboxOverride("panel", style);
        center.AddChild(panel);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 15);
        panel.AddChild(content);
        var title = new Label { Text = "삼국지 시나리오" , HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 27);
        content.AddChild(title);
        content.AddChild(new HSeparator());
        var mapPath = ProjectSettings.GlobalizePath("user://map-presets/east-asia.json");
        var mapChoice = new OptionButton();
        mapChoice.AddItem("기본 지도");
        if (File.Exists(mapPath)) mapChoice.AddItem("편집한 동아시아 지도");
        content.AddChild(mapChoice);
        var error = new Label { Text = "", Modulate = new Color("f0a486") };
        content.AddChild(error);
        var start = new Button { Text = "새 게임", CustomMinimumSize = new Vector2(0, 48) };
        start.Pressed += () =>
        {
            if (mapChoice.Selected == 1)
            {
                try
                {
                    var preset = SanguoSLG.Core.Data.ScenarioMapPresetStore.Load(mapPath);
                    if (!preset.Sites.Any(site => site.Kind is "city" or "port"))
                        throw new InvalidDataException("지도에 성 또는 항구가 없습니다.");
                }
                catch (Exception failure)
                {
                    error.Text = "맵을 열 수 없습니다: " + failure.Message;
                    return;
                }
            }
            SelectedPresetPath = mapChoice.Selected == 1 ? mapPath : null;
            _launchRequested = true;
            GetTree().ReloadCurrentScene();
        };
        content.AddChild(start);
        var map = new Button { Text = "맵 변경하기", CustomMinimumSize = new Vector2(0, 42) };
        map.Pressed += () =>
        {
            FromStartMenu = true;
            GetTree().ChangeSceneToFile("res://EditMap.tscn");
        };
        content.AddChild(map);
        var general = new Button { Text = "장수 관리", CustomMinimumSize = new Vector2(0, 42) };
        general.Pressed += () =>
        {
            FromStartMenu = true;
            GetTree().ChangeSceneToFile("res://GeneralEditor.tscn");
        };
        content.AddChild(general);
        var createGeneral = new Button { Text = "신규 장수 생성", CustomMinimumSize = new Vector2(0, 42) };
        createGeneral.Pressed += () =>
        {
            FromStartMenu = true;
            GetTree().ChangeSceneToFile("res://GeneralCreator.tscn");
        };
        content.AddChild(createGeneral);
        if (OS.GetCmdlineUserArgs().Contains("--startmenuqa")) CallDeferred(nameof(RunQa));
    }

    private void RunQa()
    {
        var buttons = FindChildren("*", "Button", true, false).OfType<Button>().Select(button => button.Text).ToArray();
        var passed = buttons.Contains("새 게임") && buttons.Contains("맵 변경하기")
            && buttons.Contains("장수 관리") && buttons.Contains("신규 장수 생성");
        GD.Print($"[scenario-start-qa] passed={passed} buttons={buttons.Length}");
        GetTree().Quit(passed ? 0 : 1);
    }

    public static void ReturnToStart(SceneTree tree)
    {
        FromStartMenu = false;
        SelectedPresetPath = null;
        tree.ChangeSceneToFile("res://Main3D.tscn");
    }
}
