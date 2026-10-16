namespace SanguoSLG.Game;

using System.Linq;
using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

public sealed partial class CampaignMapScene
{
    private bool _storyAwaitingCard;
    private bool _storyOpen;
    private bool _storyReportWasVisible;
    private string? _storyOpenId;
    private CanvasLayer? _storySpeechLayer;
    private double _storySpeechRemaining;
    private double _storySpeechClock;
    private int _storySpeechIndex;
    private GeneralStoryEvent[] _storySpeechEvents = [];

    private void PrepareStoryAfterAdvance()
    {
        var stories = _state.StoryPending.Where(e => e.Faction == Player)
            .OrderBy(e => e.Priority).ThenBy(e => e.Id, System.StringComparer.Ordinal).ToArray();
        if (stories.Length == 0) return;
        foreach (var story in stories)
            Report($"[장수 대화] {StoryTitle(story)} · {StorySpeakerName(story.Speaker)}", Gold, story.Speaker);
        // 한 진행당 한 장면만 띄운다. 나머지는 전체 보고에서 다시 읽을 수 있다.
        foreach (var story in stories.Skip(1))
            _state = GeneralStorySelector.Resolve(_state, story.Id, "report_only");
        _storyAwaitingCard = true;
    }

    private void TickStoryPresentation(double delta)
    {
        if (_storySpeechLayer is not null)
        {
            _storySpeechRemaining -= delta;
            if (_storySpeechRemaining <= 0)
            {
                _storySpeechLayer.QueueFree();
                _storySpeechLayer = null;
            }
        }
        if (_advancing && _storySpeechIndex < _storySpeechEvents.Length)
        {
            _storySpeechClock += delta;
            if (_storySpeechClock >= (_storySpeechIndex + 1) * 2.5)
                ShowStorySpeech(_storySpeechEvents[_storySpeechIndex++]);
        }
        if (_storyAwaitingCard && !_advancing && !_storyOpen
            && _modalLayer is null && _confirmLayer is null)
        {
            _storyAwaitingCard = false;
            var next = _state.StoryPending.Where(e => e.Faction == Player)
                .OrderBy(e => e.Priority).ThenBy(e => e.Id, System.StringComparer.Ordinal).FirstOrDefault();
            if (next is not null) ShowStoryModal(next, false);
        }
    }

    private void ShowStorySpeech(GeneralStoryEvent story)
    {
        if (_storySpeechLayer is not null) { _storySpeechLayer.QueueFree(); _storySpeechLayer = null; }
        var camera = GetViewport().GetCamera3D();
        if (camera is null) return;
        Vector3? world = _state.Armies.FirstOrDefault(u => u.VanguardId == story.Speaker
            || u.AdjutantId == story.Speaker) is { } unit
                && _armyTokens.TryGetValue(unit.Id.Value, out var token) && IsInstanceValid(token)
            ? token.GlobalPosition : null;
        if (world is null && story.City is { } cityId
            && _cityModels.TryGetValue(cityId.Value, out var cityModel) && IsInstanceValid(cityModel))
            world = cityModel.GlobalPosition;
        if (world is null || camera.IsPositionBehind(world.Value)) return;
        var point = camera.UnprojectPosition(world.Value + Vector3.Up * 1.1f);
        var layer = new CanvasLayer { Layer = 31 };
        AddChild(layer);
        var speech = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        speech.AddThemeStyleboxOverride("panel", Frame(Ink, Gold, 1, 9, 8));
        layer.AddChild(speech);
        speech.AddChild(MakeLabel($"{StorySpeakerName(story.Speaker)}: {StoryShortLine(story)}", 12, Parchment));
        speech.Position = point + new Vector2(-80, -36);
        _storySpeechLayer = layer;
        _storySpeechRemaining = 2.2;
    }

    private void ShowStoryModal(GeneralStoryEvent story, bool replay)
    {
        if (_modalLayer is not null) return;
        _storyOpen = true;
        _storyOpenId = replay ? null : story.Id;
        _storyReportWasVisible = _reportPanel.Visible;
        _reportPanel.Visible = false;
        var layer = new CanvasLayer { Layer = 45 };
        AddChild(layer);
        _modalLayer = layer;
        var backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.16f), MouseFilter = Control.MouseFilterEnum.Stop };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        backdrop.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) CloseStoryModal();
        };
        layer.AddChild(backdrop);
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        panel.AddThemeStyleboxOverride("panel", Frame(Ink, Gold, 2, 10, 12));
        panel.AnchorLeft = 0.05f; panel.AnchorRight = 0.95f;
        panel.AnchorTop = 0.70f; panel.AnchorBottom = 0.98f;
        layer.AddChild(panel);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);
        panel.AddChild(row);
        var portraitColumn = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        portraitColumn.SizeFlagsStretchRatio = 3;
        row.AddChild(portraitColumn);
        portraitColumn.AddChild(new TextureRect
        {
            Texture = PortraitFor(story.Speaker) ?? CircularPortraitFor(story.Speaker),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        portraitColumn.AddChild(MakeLabel(StorySpeakerName(story.Speaker), 14, GoldBright));
        var content = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        content.SizeFlagsStretchRatio = 7;
        row.AddChild(content);
        var heading = new HBoxContainer();
        content.AddChild(heading);
        var title = MakeLabel(StoryTitle(story), 18, GoldBright);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        heading.AddChild(title);
        var close = MakeButton("✕");
        close.CustomMinimumSize = new Vector2(34, 28);
        close.Pressed += CloseStoryModal;
        heading.AddChild(close);
        content.AddChild(GoldRule());
        var lines = StoryLines(story);
        var line = MakeLabel(lines[0], 15, Parchment);
        line.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        line.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        content.AddChild(line);
        var currentPage = 0;
        var next = MakeButton(replay ? "다음" : "다음", accent: true);
        next.Pressed += () =>
        {
            if (++currentPage < lines.Length) line.Text = lines[currentPage];
            else CloseStoryModal();
        };
        content.AddChild(next);
    }

    private void CloseStoryModal()
    {
        if (!_storyOpen) return;
        _storyOpen = false;
        if (_storyOpenId is { } id) _state = GeneralStorySelector.Resolve(_state, id, null);
        _storyOpenId = null;
        if (_modalLayer is not null) { _modalLayer.QueueFree(); _modalLayer = null; }
        if (IsInstanceValid(_reportPanel)) _reportPanel.Visible = _storyReportWasVisible;
    }

    private void AddStoryReplayButtons(VBoxContainer body)
    {
        var stories = _state.StoryHistory.Where(e => e.Faction == Player)
            .OrderByDescending(e => e.Day).Take(30).ToArray();
        if (stories.Length == 0) return;
        body.AddChild(GoldRule());
        body.AddChild(MakeLabel("지난 장수 대화", 16, GoldBright));
        foreach (var story in stories)
        {
            var saved = story;
            var button = MakeButton($"{story.Day}일 · {StoryTitle(story)} · {StorySpeakerName(story.Speaker)}");
            button.Pressed += () => { CloseModal(); ShowStoryModal(saved, true); };
            body.AddChild(button);
        }
    }

    private string StorySpeakerName(GeneralId id)
        => _state.Generals.FirstOrDefault(g => g.Id == id)?.Name ?? "장수";

    private static string StoryTitle(GeneralStoryEvent story) => story.Kind switch
    {
        "capture" => "새 성의 첫날", "victory" => "승전 논공", "beast_defeated" => "신수 사냥 뒤의 대화",
        "beast_appeared" => "신수의 흔적", "growth" => "장수 성장", "research" => "연구 완수 토론",
        "production" => "생산 귀환", "diplomacy" => "외교 보고", _ => "전장 소식",
    };

    private static string StoryShortLine(GeneralStoryEvent story)
        => story.Kind switch
        {
            "capture" => "새 성을 얻었습니다.", "victory" => "적을 물리쳤습니다!",
            "beast_defeated" => "신수를 쓰러뜨렸습니다.", "growth" => "한층 성장했습니다.",
            _ => "새로운 소식이 있습니다.",
        };

    private string[] StoryLines(GeneralStoryEvent story)
        => [$"{StorySpeakerName(story.Speaker)}: {StoryShortLine(story)}",
            story.SecondSpeaker is { } second
                ? $"{StorySpeakerName(second)}: 이 성과를 함께 기억하겠습니다."
                : "이 결과를 보고에 남기겠습니다."];
}
