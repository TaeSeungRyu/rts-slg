namespace SanguoSLG.Game;

using System.Linq;
using Godot;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

public sealed partial class CampaignMapScene
{
    private bool _storyAwaitingCard;
    private IReadOnlyList<GeneralStoryDefinition> _storyDefinitions = [];
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

    private string StoryTitle(GeneralStoryEvent story)
        => _storyDefinitions.FirstOrDefault(d => d.Kind == story.Kind)?.Title ?? "전장 소식";

    private string StoryShortLine(GeneralStoryEvent story)
        => StoryLines(story)[0].Replace($"{StorySpeakerName(story.Speaker)}: ", "");

    private string[] StoryLines(GeneralStoryEvent story)
    {
        var definition = _storyDefinitions.FirstOrDefault(d => d.Kind == story.Kind);
        if (definition is null) return [$"{StorySpeakerName(story.Speaker)}: 새로운 소식이 있습니다.", "보고에 기록했습니다."];
        var city = story.City is { } cityId
            ? _state.Cities.FirstOrDefault(c => c.Id == cityId)?.Name ?? "도시" : "도시";
        var second = story.SecondSpeaker is { } secondId ? StorySpeakerName(secondId) : StorySpeakerName(story.Speaker);
        var result = definition.Variants[story.Variant % definition.Variants.Count]
            .Select(line => line.Replace("{speaker}", StorySpeakerName(story.Speaker))
                .Replace("{second}", second).Replace("{city}", city)
                .Replace("{amount}", story.Amount.ToString("N0"))).ToList();
        if (definition.VoiceLines is not null
            && definition.VoiceLines.TryGetValue(story.Speaker.Value, out var voice)
            && result.Count < 4) result.Add($"{StorySpeakerName(story.Speaker)}: {voice}");
        return result.ToArray();
    }
}
