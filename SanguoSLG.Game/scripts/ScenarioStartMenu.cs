using Godot;

namespace SanguoSLG.Game;

public sealed partial class ScenarioStartMenu : Control
{
    private static bool _launchRequested;
    public static bool CampaignStartedFromMenu { get; private set; }
    public static string? SelectedPresetPath { get; private set; }
    public static string? SelectedScenarioPath { get; private set; }
    public static int SelectedPlayerFactionId { get; private set; } = 1;
    public static string? SelectedScenarioTitle { get; private set; }
    public static SanguoSLG.Core.Simulation.GameDifficulty SelectedDifficulty { get; private set; }
    public static bool LoadSlotsOnStart { get; private set; }
    public static bool FromStartMenu { get; private set; }

    public static bool ConsumeLaunchRequest()
    {
        if (!_launchRequested) return false;
        _launchRequested = false;
        return true;
    }

    public static void EnterEditor(SceneTree tree, string scene)
    {
        _launchRequested = false;
        LoadSlotsOnStart = false;
        SelectedPresetPath = null;
        SelectedScenarioPath = null;
        FromStartMenu = true;
        tree.AutoAcceptQuit = true;
        tree.ChangeSceneToFile(scene);
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        var background = new ColorRect();
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        background.Material = new ShaderMaterial { Shader = new Shader { Code = """
shader_type canvas_item;
void fragment() {
    vec2 p = UV;
    float drift = sin(p.x * 9.0 + TIME * 0.16) * 0.025;
    float ridge = smoothstep(0.53, 0.56, p.y + sin(p.x * 9.0 + TIME * 0.05) * 0.028);
    float near_ridge = smoothstep(0.76, 0.79, p.y + sin(p.x * 15.0 - TIME * 0.035) * 0.018);
    vec3 sky = mix(vec3(0.075, 0.15, 0.16), vec3(0.18, 0.27, 0.25), p.y + drift);
    COLOR = vec4(mix(mix(sky, vec3(0.09, 0.16, 0.15), ridge * 0.65), vec3(0.045, 0.085, 0.08), near_ridge * 0.8), 1.0);
}
""" } };
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
        center.Visible = false;
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 15);
        panel.AddChild(content);
        var title = new Label { Text = "새 게임 시작하기" , HorizontalAlignment = HorizontalAlignment.Center };
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
            .GroupBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.FirstOrDefault(path => path.StartsWith(scenarioDir, StringComparison.OrdinalIgnoreCase)) ?? group.First())
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
                        .Where(f => f.Playable && !f.Hidden).Select(f => (f.Id, f.Name));
                foreach (var (id, name) in factions) factionChoice.AddItem(name, id);
                factionChoice.Select(0);
            }
            catch (Exception failure) { GD.PushWarning("세력 목록: " + failure.Message); }
        }
        scenarioChoice.ItemSelected += _ => RefreshFactions();
        RefreshFactions();
        content.AddChild(new Label { Text = "기본 지도" });
        var mapPath = ScenarioRootMapSource.Path;
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
                    ? ScenarioRootMapSource.Load()
                    : LoadScenarioMap(chosen, scenarioDir);
                preview.Text = $"지도 {preset.Width}×{preset.Height} · 성 {preset.Sites.Count(s => s.Kind == "city")} · 항구 {preset.Sites.Count(s => s.Kind == "port")}";
            }
            catch (Exception failure) { preview.Text = "지도 미리보기 오류: " + failure.Message; }
        }
        scenarioChoice.ItemSelected += _ => RefreshPreview();
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
            if (factionChoice.ItemCount == 0)
            {
                error.Text = "시작 가능한 세력이 없습니다.";
                return;
            }
            try
            {
                var data = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "data"));
                var source = CreatedGeneralPresets.ApplyAtNewGame(
                    new SanguoSLG.Core.Data.ScenarioLoader().LoadFromDirectory(data));
                var archive = new SanguoSLG.Core.Data.ScenarioMapArchive(
                    ProjectSettings.GlobalizePath("user://maps"), scenarioDir);
                var selectedPath = scenarioPaths[scenarioChoice.Selected];
                var map = selectedPath is null ? ScenarioRootMapSource.OpenVersion(archive) : null;
                var draft = selectedPath is null
                    ? SanguoSLG.Core.Data.ScenarioDraftApplier.CreateSeed("default-190", "190년 기본", source, map!)
                    : SanguoSLG.Core.Data.ScenarioDraftStore.Load(selectedPath);
                if (map is null)
                {
                    if (SanguoSLG.Core.Data.ScenarioRootMapBinding.UsesRoot(draft.MapId))
                    {
                        map = ScenarioRootMapSource.OpenVersion(archive);
                        draft = SanguoSLG.Core.Data.ScenarioRootMapBinding.Bind(draft, map,
                            ScenarioRootMapSource.PreviousVersion(archive, draft));
                    }
                    else map = archive.Load(draft.MapId, draft.MapRevision);
                }
                SanguoSLG.Core.Data.ScenarioDraftApplier.Apply(source, draft, map.Preset);
            }
            catch (Exception failure)
            {
                error.Text = "시나리오 시작 불가: " + failure.Message;
                return;
            }
            SelectedPresetPath = scenarioChoice.Selected == 0 ? mapPath : null;
            SelectedScenarioPath = scenarioPaths[scenarioChoice.Selected];
            SelectedPlayerFactionId = factionChoice.GetSelectedId();
            SelectedScenarioTitle = scenarioChoice.GetItemText(scenarioChoice.Selected);
            SelectedDifficulty = (SanguoSLG.Core.Simulation.GameDifficulty)difficulty.GetSelectedId();
            _launchRequested = true;
            CampaignStartedFromMenu = true;
            GetTree().ReloadCurrentScene();
        };
        content.AddChild(start);
        var menuCenter = new CenterContainer();
        menuCenter.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(menuCenter);
        var menuPanel = new PanelContainer { CustomMinimumSize = new Vector2(440, 0) };
        menuPanel.AddThemeStyleboxOverride("panel", style);
        menuCenter.AddChild(menuPanel);
        var menu = new VBoxContainer();
        menu.AddThemeConstantOverride("separation", 11);
        menuPanel.AddChild(menu);
        var gameTitle = new Label { Text = "삼국지", HorizontalAlignment = HorizontalAlignment.Center };
        gameTitle.AddThemeFontSizeOverride("font_size", 40);
        gameTitle.AddThemeColorOverride("font_color", new Color("e5c985"));
        menu.AddChild(gameTitle);
        menu.AddChild(new Label { Text = "영웅의 시대", HorizontalAlignment = HorizontalAlignment.Center });
        menu.AddChild(new HSeparator());
        void MenuItem(string label, Action pressed)
        {
            var button = new Button { Text = label, CustomMinimumSize = new Vector2(0, 46) };
            button.AddThemeFontSizeOverride("font_size", 17);
            button.Pressed += pressed;
            menu.AddChild(button);
        }
        MenuItem("새 게임 시작하기", () => { menuCenter.Visible = false; center.Visible = true; });
        MenuItem("저장된 게임 시작하기", () => OpenLoadSlots(menuCenter));
        MenuItem("신규장수 등록", () => EnterEditor(GetTree(), "res://GeneralCreator.tscn"));
        MenuItem("장수 에디터", () => EnterEditor(GetTree(), "res://GeneralEditor.tscn"));
        MenuItem("지형 에디터", () => EnterEditor(GetTree(), "res://EditMap.tscn"));
        MenuItem("시나리오 에디터", () => EnterEditor(GetTree(), "res://ScenarioEditor.tscn"));
        var back = new Button { Text = "◀ 메인 화면", CustomMinimumSize = new Vector2(0, 36) };
        back.Pressed += () => { center.Visible = false; menuCenter.Visible = true; };
        content.AddChild(back);
        ShowOptionalIntro(menuCenter);
        if (OS.GetCmdlineUserArgs().Contains("--startmenuqa")) CallDeferred(nameof(RunQa));
    }

    private static SanguoSLG.Core.Data.ScenarioMapPreset LoadScenarioMap(
        SanguoSLG.Core.Data.ScenarioDraft draft, string scenarioDir)
    {
        if (SanguoSLG.Core.Data.ScenarioRootMapBinding.UsesRoot(draft.MapId))
            return ScenarioRootMapSource.Load();
        return new SanguoSLG.Core.Data.ScenarioMapArchive(
            ProjectSettings.GlobalizePath("user://maps"), scenarioDir)
            .Load(draft.MapId, draft.MapRevision).Preset;
    }

    private void RunQa()
    {
        var buttons = FindChildren("*", "Button", true, false).OfType<Button>().Select(button => button.Text).ToArray();
        var scenarios = FindChildren("*", "OptionButton", true, false).OfType<OptionButton>().FirstOrDefault();
        var passed = buttons.Contains("게임 시작") && buttons.Contains("새 게임 시작하기")
            && scenarios?.ItemCount >= 1
            && buttons.Contains("지형 에디터")
            && buttons.Contains("시나리오 에디터") && buttons.Contains("장수 에디터")
            && buttons.Contains("신규장수 등록") && buttons.Contains("저장된 게임 시작하기");
        GD.Print($"[scenario-start-qa] passed={passed} buttons={buttons.Length}");
        GetTree().Quit(passed ? 0 : 1);
    }

    public static void ReturnToStart(SceneTree tree)
    {
        CampaignMapScene.ClearPendingLoadedState();
        _launchRequested = false;
        CampaignStartedFromMenu = false;
        FromStartMenu = false;
        SelectedPresetPath = null;
        SelectedScenarioPath = null;
        LoadSlotsOnStart = false;
        SelectedPlayerFactionId = 1;
        SelectedDifficulty = SanguoSLG.Core.Simulation.GameDifficulty.Normal;
        tree.ChangeSceneToFile("res://Main3D.tscn");
    }
}
