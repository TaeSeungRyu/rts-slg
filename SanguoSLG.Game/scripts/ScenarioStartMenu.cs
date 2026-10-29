using Godot;

namespace SanguoSLG.Game;

public sealed partial class ScenarioStartMenu : Control
{
    private static bool _launchRequested;
    public static string? SelectedPresetPath { get; private set; }
    public static string? SelectedScenarioPath { get; private set; }
    public static int SelectedPlayerFactionId { get; private set; } = 1;
    public static SanguoSLG.Core.Simulation.GameDifficulty SelectedDifficulty { get; private set; }
    public static bool LoadSlotsOnStart { get; private set; }
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
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(500, 460) };
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
        content.AddChild(new Label { Text = "시나리오" });
        var scenarioChoice = new OptionButton();
        scenarioChoice.AddItem("190년 기본 시나리오");
        var scenarioPaths = new List<string?> { null };
        var scenarioDir = ProjectSettings.GlobalizePath("user://scenarios");
        var builtInDir = ProjectSettings.GlobalizePath("res://assets/scenarios");
        var builtInPaths = Directory.Exists(builtInDir) ? Directory.EnumerateFiles(builtInDir, "*.json") : [];
        var userPaths = Directory.Exists(scenarioDir) ? Directory.EnumerateFiles(scenarioDir, "*.json") : [];
        foreach (var path in builtInPaths.Concat(userPaths)
            .OrderBy(path => Path.GetFileName(path).StartsWith("fictional-", StringComparison.Ordinal) ? 9999
                : SanguoSLG.Core.Data.ScenarioDraftStore.Load(path).StartYear).ThenBy(path => path))
                try
                {
                    var draft = SanguoSLG.Core.Data.ScenarioDraftStore.Load(path);
                    scenarioChoice.AddItem(draft.Title);
                    scenarioPaths.Add(path);
                }
                catch (Exception failure) { GD.PushWarning("시나리오 목록: " + failure.Message); }
        content.AddChild(scenarioChoice);
        var factionChoice = new OptionButton();
        content.AddChild(factionChoice);
        void RefreshFactions()
        {
            factionChoice.Clear();
            try
            {
                var selectedPath = scenarioPaths[scenarioChoice.Selected];
                var factions = selectedPath is null
                    ? new SanguoSLG.Core.Data.ScenarioLoader().LoadFromDirectory(
                        Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "data")))
                        .Factions.Select(f => (f.Id.Value, f.Name))
                    : SanguoSLG.Core.Data.ScenarioDraftStore.Load(selectedPath).Factions
                        .Where(f => f.Playable).Select(f => (f.Id, f.Name));
                foreach (var (id, name) in factions) factionChoice.AddItem(name, id);
                factionChoice.Select(0);
            }
            catch (Exception failure) { GD.PushWarning("세력 목록: " + failure.Message); }
        }
        scenarioChoice.ItemSelected += _ => RefreshFactions();
        RefreshFactions();
        content.AddChild(new Label { Text = "기본 시나리오의 지도" });
        var mapPath = ProjectSettings.GlobalizePath("user://map-presets/east-asia.json");
        var mapChoice = new OptionButton();
        mapChoice.AddItem("기본 지도");
        if (File.Exists(mapPath)) mapChoice.AddItem("편집한 동아시아 지도");
        content.AddChild(mapChoice);
        var preview = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        content.AddChild(preview);
        void RefreshPreview()
        {
            try
            {
                var selectedPath = scenarioPaths[scenarioChoice.Selected];
                var chosen = selectedPath is null ? null
                    : SanguoSLG.Core.Data.ScenarioDraftStore.Load(selectedPath);
                var preset = chosen is null
                    ? SanguoSLG.Core.Data.ScenarioMapPresetStore.Load(
                        mapChoice.Selected == 1 ? mapPath : ProjectSettings.GlobalizePath("res://assets/maps/east-asia-user-preset.json"))
                    : LoadScenarioMap(chosen, scenarioDir);
                preview.Text = $"지도 {preset.Width}×{preset.Height} · 성 {preset.Sites.Count(s => s.Kind == "city")} · 항구 {preset.Sites.Count(s => s.Kind == "port")}";
            }
            catch (Exception failure) { preview.Text = "지도 미리보기 오류: " + failure.Message; }
        }
        scenarioChoice.ItemSelected += _ => RefreshPreview();
        mapChoice.ItemSelected += _ => RefreshPreview();
        RefreshPreview();
        var difficulty = new OptionButton();
        difficulty.AddItem("난이도: 보통", 0);
        difficulty.AddItem("난이도: 어려움", 1);
        content.AddChild(difficulty);
        var error = new Label { Text = "", Modulate = new Color("f0a486") };
        content.AddChild(error);
        var start = new Button { Text = "게임 시작", CustomMinimumSize = new Vector2(0, 48) };
        start.Pressed += () =>
        {
            if (scenarioChoice.Selected == 0 && mapChoice.Selected == 1)
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
            SelectedPresetPath = scenarioChoice.Selected == 0 && mapChoice.Selected == 1 ? mapPath : null;
            SelectedScenarioPath = scenarioPaths[scenarioChoice.Selected];
            SelectedPlayerFactionId = factionChoice.GetSelectedId();
            SelectedDifficulty = (SanguoSLG.Core.Simulation.GameDifficulty)difficulty.GetSelectedId();
            _launchRequested = true;
            GetTree().ReloadCurrentScene();
        };
        content.AddChild(start);
        var load = new Button { Text = "게임 불러오기", CustomMinimumSize = new Vector2(0, 36) };
        load.Pressed += () =>
        {
            LoadSlotsOnStart = true;
            SelectedScenarioPath = null;
            SelectedPresetPath = null;
            SelectedPlayerFactionId = 1;
            SelectedDifficulty = SanguoSLG.Core.Simulation.GameDifficulty.Normal;
            _launchRequested = true;
            GetTree().ReloadCurrentScene();
        };
        content.AddChild(load);
        var scenarioEditor = new Button { Text = "시나리오 에디터", CustomMinimumSize = new Vector2(0, 36) };
        scenarioEditor.Pressed += () => { FromStartMenu = true; GetTree().ChangeSceneToFile("res://ScenarioEditor.tscn"); };
        content.AddChild(scenarioEditor);
        var map = new Button { Text = "지도 에디터", CustomMinimumSize = new Vector2(0, 36) };
        map.Pressed += () =>
        {
            FromStartMenu = true;
            GetTree().ChangeSceneToFile("res://EditMap.tscn");
        };
        content.AddChild(map);
        var general = new Button { Text = "장수 편집", CustomMinimumSize = new Vector2(0, 36) };
        general.Pressed += () =>
        {
            FromStartMenu = true;
            GetTree().ChangeSceneToFile("res://GeneralEditor.tscn");
        };
        content.AddChild(general);
        var createGeneral = new Button { Text = "신규 장수 추가", CustomMinimumSize = new Vector2(0, 36) };
        createGeneral.Pressed += () =>
        {
            FromStartMenu = true;
            GetTree().ChangeSceneToFile("res://GeneralCreator.tscn");
        };
        content.AddChild(createGeneral);
        content.MoveChild(general, content.GetChildCount() - 4);
        content.MoveChild(createGeneral, content.GetChildCount() - 3);
        if (OS.GetCmdlineUserArgs().Contains("--startmenuqa")) CallDeferred(nameof(RunQa));
    }

    private static SanguoSLG.Core.Data.ScenarioMapPreset LoadScenarioMap(
        SanguoSLG.Core.Data.ScenarioDraft draft, string scenarioDir)
    {
        if (draft.MapId == "default-east-asia")
        {
            var preset = SanguoSLG.Core.Data.ScenarioMapPresetStore.Load(
                ProjectSettings.GlobalizePath("res://assets/maps/east-asia-user-preset.json"));
            if (SanguoSLG.Core.Data.ScenarioMapArchive.RevisionOf(preset) == draft.MapRevision)
                return preset;
        }
        return new SanguoSLG.Core.Data.ScenarioMapArchive(
            ProjectSettings.GlobalizePath("user://maps"), scenarioDir)
            .Load(draft.MapId, draft.MapRevision).Preset;
    }

    private void RunQa()
    {
        var buttons = FindChildren("*", "Button", true, false).OfType<Button>().Select(button => button.Text).ToArray();
        var scenarios = FindChildren("*", "OptionButton", true, false).OfType<OptionButton>().FirstOrDefault();
        var passed = buttons.Contains("게임 시작") && buttons.Contains("게임 불러오기")
            && scenarios?.ItemCount == 7
            && buttons.Contains("지도 에디터")
            && buttons.Contains("시나리오 에디터") && buttons.Contains("장수 편집")
            && buttons.Contains("신규 장수 추가")
            && buttons.TakeLast(6).SequenceEqual(new[] { "게임 시작", "게임 불러오기",
                "장수 편집", "신규 장수 추가", "시나리오 에디터", "지도 에디터" });
        GD.Print($"[scenario-start-qa] passed={passed} buttons={buttons.Length}");
        GetTree().Quit(passed ? 0 : 1);
    }

    public static void ReturnToStart(SceneTree tree)
    {
        CampaignMapScene.ClearPendingLoadedState();
        FromStartMenu = false;
        SelectedPresetPath = null;
        SelectedScenarioPath = null;
        LoadSlotsOnStart = false;
        SelectedDifficulty = SanguoSLG.Core.Simulation.GameDifficulty.Normal;
        tree.ChangeSceneToFile("res://Main3D.tscn");
    }
}
