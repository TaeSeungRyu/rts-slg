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
    private PanelContainer? _storySpeechPanel;
    private GeneralStoryEvent? _storySpeechAnchor;
    private double _storySpeechRemaining;
    private double _storySpeechClock;
    private int _storySpeechIndex;
    private GeneralStoryEvent[] _storySpeechEvents = [];

    private GeneralStoryEvent[] CombatSpeechEvents(GameState before, IReadOnlyList<AdvanceTurn> turns)
    {
        var units = before.Armies.Concat(turns.SelectMany(turn => turn.Units))
            .GroupBy(unit => unit.Id).ToDictionary(group => group.Key, group => group.First());
        return turns.SelectMany((turn, day) => (turn.Combat?.Exchanges ?? [])
                .Where(exchange => exchange.Amount > 0)
                .Select(exchange => (exchange.Attacker, day)))
            .Distinct()
            .OrderBy(entry => entry.day).ThenBy(entry => entry.Attacker.Value)
            .Where(entry => units.TryGetValue(entry.Attacker, out var unit)
                && unit.Field.Owner == Player && unit.VanguardId is not null
                && ((long)before.Day * 31 + entry.day * 17 + entry.Attacker.Value) % 2 == 0)
            .Take(3)
            .Select(entry =>
            {
                var unit = units[entry.Attacker];
                return new GeneralStoryEvent($"combat-bark:{before.Day}:{entry.day}:{entry.Attacker.Value}",
                    "combat_bark", before.Day + entry.day, 3, Player, unit.VanguardId!.Value,
                    unit.AdjutantId, null, (before.Day + entry.day + entry.Attacker.Value) % 10);
            }).ToArray();
    }

    private GeneralStoryEvent[] CitySpeechEvents(GameState before)
    {
        // 한 진행에 최대 한 도시의 장수만 말한다. 교전 대사와 섞여 화면을 덮지 않는다.
        if (before.Day % 3 == 1) return [];
        var candidates = before.Assignments
            .Where(posting => posting.Faction == Player && posting.Location is not null
                && before.Cities.Any(city => city.Id == posting.Location && city.Owner == Player))
            .OrderBy(posting => posting.Location.GetValueOrDefault().Value)
            .ThenBy(posting => posting.General.Value).ToArray();
        if (candidates.Length == 0) return [];
        var selected = candidates[before.Day % candidates.Length];
        return [new GeneralStoryEvent($"city-bark:{before.Day}:{selected.General.Value}",
            "city_bark", before.Day, 8, Player, selected.General, null, selected.Location,
            before.Day % 20)];
    }

    private void PrepareStoryAfterAdvance()
    {
        var stories = _state.StoryPending.Where(e => e.Faction == Player)
            .OrderBy(e => e.Priority).ThenBy(e => e.Id, System.StringComparer.Ordinal).ToArray();
        if (stories.Length == 0) return;
        foreach (var story in stories)
            Report($"[장수 대화] {StoryTitle(story)} · {StorySpeakerName(story.Speaker)}", Gold,
                story.Speaker, story.Id);
        // 여러 사건이 같은 날 발생해도 결과 대화를 보고 전용으로 버리지 않는다.
        _storyAwaitingCard = true;
    }

    private GeneralStoryEvent? NextPendingStory()
        => _state.StoryPending.Where(e => e.Faction == Player)
            .OrderBy(e => e.Kind.StartsWith("alliance_", System.StringComparison.Ordinal) ? 0
                : e.Kind.StartsWith("scout_", System.StringComparison.Ordinal)
                    || e.Kind.StartsWith("stratagem_", System.StringComparison.Ordinal) ? 1 : 2)
            .ThenBy(e => e.Priority).ThenBy(e => e.Id, System.StringComparer.Ordinal).FirstOrDefault();

    private void TickStoryPresentation(double delta)
    {
        if (_storySpeechLayer is not null)
        {
            UpdateStorySpeechPosition();
            _storySpeechRemaining -= delta;
            if (_storySpeechRemaining <= 0)
            {
                _storySpeechLayer.QueueFree();
                _storySpeechLayer = null;
                _storySpeechPanel = null;
                _storySpeechAnchor = null;
            }
        }
        if (_advancing && _storySpeechIndex < _storySpeechEvents.Length)
        {
            _storySpeechClock += delta;
            if (_storySpeechClock >= (_storySpeechIndex + 1) * 1.8)
                ShowStorySpeech(_storySpeechEvents[_storySpeechIndex++]);
        }
        if (_storyAwaitingCard && !_advancing && !_storyOpen
            && _modalLayer is null && _confirmLayer is null)
        {
            _storyAwaitingCard = false;
            var next = NextPendingStory();
            if (next is not null) ShowStoryModal(next, false);
        }
    }

    private void ShowStorySpeech(GeneralStoryEvent story)
    {
        if (_storySpeechLayer is not null) { _storySpeechLayer.QueueFree(); _storySpeechLayer = null; }
        var camera = GetViewport().GetCamera3D();
        if (camera is null) return;
        _storySpeechAnchor = story;
        Vector3? world = StorySpeechWorldPosition(story);
        if (world is null || camera.IsPositionBehind(world.Value)) return;
        var layer = new CanvasLayer { Layer = 31 };
        AddChild(layer);
        var speech = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        speech.AddThemeStyleboxOverride("panel", Frame(Ink, Gold, 1, 9, 8));
        layer.AddChild(speech);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 7);
        speech.AddChild(row);
        row.AddChild(new TextureRect
        {
            Name = "StorySpeechPortrait",
            Texture = CircularPortraitFor(story.Speaker),
            CustomMinimumSize = new Vector2(30, 30),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        var text = MakeLabel($"{StorySpeakerName(story.Speaker)}  {StoryShortLine(story)}", 12, Parchment);
        text.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.AddChild(text);
        _storySpeechLayer = layer;
        _storySpeechPanel = speech;
        _storySpeechRemaining = 2.2;
        UpdateStorySpeechPosition();
    }

    private Vector3? StorySpeechWorldPosition(GeneralStoryEvent story)
    {
        Vector3? world = _state.Armies.FirstOrDefault(u => u.VanguardId == story.Speaker
            || u.AdjutantId == story.Speaker) is { } unit
                && _armyTokens.TryGetValue(unit.Id.Value, out var token) && IsInstanceValid(token)
            ? token.GlobalPosition : null;
        if (world is null && story.City is { } cityId
            && _cityModels.TryGetValue(cityId.Value, out var cityModel) && IsInstanceValid(cityModel))
            world = cityModel.GlobalPosition;
        return world;
    }

    private void UpdateStorySpeechPosition()
    {
        if (_storySpeechPanel is null || _storySpeechAnchor is null) return;
        var camera = GetViewport().GetCamera3D();
        var world = StorySpeechWorldPosition(_storySpeechAnchor);
        _storySpeechPanel.Visible = camera is not null && world is not null
            && !camera.IsPositionBehind(world.Value);
        if (_storySpeechPanel.Visible)
            _storySpeechPanel.Position = camera!.UnprojectPosition(world!.Value + Vector3.Up * 1.1f)
                + new Vector2(-80, -36);
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
        panel.AnchorLeft = 0.14f; panel.AnchorRight = 0.86f;
        panel.AnchorTop = 0.70f; panel.AnchorBottom = 0.98f;
        panel.ClipContents = true;
        layer.AddChild(panel);
        var frame = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        panel.AddChild(frame);
        var heading = new HBoxContainer();
        frame.AddChild(heading);
        var title = MakeLabel(StoryTitle(story), 18, GoldBright);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        heading.AddChild(title);
        var close = MakeButton("✕");
        close.CustomMinimumSize = new Vector2(34, 28);
        close.Pressed += CloseStoryModal;
        heading.AddChild(close);
        frame.AddChild(GoldRule());
        var row = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, ClipContents = true };
        row.Name = "StoryDialogueRow";
        row.AddThemeConstantOverride("separation", 16);
        frame.AddChild(row);
        var diplomacy = story.Kind.StartsWith("alliance_", System.StringComparison.Ordinal);
        var portraitColumn = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        portraitColumn.SizeFlagsStretchRatio = diplomacy ? 2 : 3;
        row.AddChild(portraitColumn);
        var portrait = new TextureRect
        {
            Texture = PortraitFor(story.Speaker) ?? CircularPortraitFor(story.Speaker),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            // 하단 카드의 높이는 짧다. Covered는 원본의 중앙을 잘라 얼굴/머리 장식을 가린다.
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        portraitColumn.AddChild(portrait);
        var portraitName = MakeLabel(StorySpeakerName(story.Speaker), 14, GoldBright);
        portraitName.Name = "StoryCurrentSpeaker";
        portraitColumn.AddChild(portraitName);
        VBoxContainer? audienceColumn = null;
        Label? audienceName = null;
        TextureRect? rulerPortrait = null;
        TextureRect? advisorPortrait = null;
        if (diplomacy && story.SecondSpeaker is { } rulerId)
        {
            audienceColumn = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            audienceColumn.SizeFlagsStretchRatio = 2;
            row.AddChild(audienceColumn);
            rulerPortrait = new TextureRect
            {
                Name = "DiplomacyRulerPortrait",
                Texture = PortraitFor(rulerId) ?? CircularPortraitFor(rulerId),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                Modulate = Colors.White,
            };
            audienceColumn.AddChild(rulerPortrait);
            if (story.Kind.EndsWith("_advisor", System.StringComparison.Ordinal) && story.Amount > 0)
            {
                advisorPortrait = new TextureRect
                {
                    Name = "DiplomacyAdvisorPortrait",
                    Texture = CircularPortraitFor(new GeneralId(story.Amount)),
                    CustomMinimumSize = Vector2.Zero,
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    Modulate = Colors.White,
                };
                audienceColumn.AddChild(advisorPortrait);
            }
            audienceName = MakeLabel(StorySpeakerName(rulerId), 13, GoldBright);
            audienceColumn.AddChild(audienceName);
        }
        var content = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        content.SizeFlagsStretchRatio = diplomacy ? 6 : 7;
        row.AddChild(content);
        if (diplomacy) row.MoveChild(content, 1); // 아군: 초상-대사, 상대: 대사-초상
        var lines = StoryLines(story);
        void HighlightSpeaker(string text)
        {
            if (!diplomacy) return;
            var current = StoryLineSpeaker(story, text);
            var isOwn = current is null || current == story.Speaker;
            portraitColumn.Visible = isOwn;
            if (audienceColumn is not null) audienceColumn.Visible = !isOwn;
            if (rulerPortrait is not null) rulerPortrait.Visible = !isOwn && current == story.SecondSpeaker;
            if (advisorPortrait is not null)
                advisorPortrait.Visible = !isOwn && current == new GeneralId(story.Amount);
            if (audienceName is not null && current is { } generalId && !isOwn)
                audienceName.Text = StorySpeakerName(generalId);
            foreach (var image in new[] { portrait, rulerPortrait, advisorPortrait })
            {
                if (image is null || !image.Visible) continue;
                var tween = CreateTween();
                image.Modulate = new Color(0.7f, 0.7f, 0.7f);
                tween.TweenProperty(image, "modulate", Colors.White, 0.15);
            }
        }
        var line = MakeLabel(lines[0], 15, Parchment);
        HighlightSpeaker(lines[0]);
        line.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        line.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        content.AddChild(line);
        var currentPage = 0;
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        content.AddChild(actions);
        actions.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var next = MakeButton("다음 ▶", accent: true);
        next.CustomMinimumSize = new Vector2(86, 34);
        next.Pressed += () =>
        {
            if (++currentPage < lines.Length)
            {
                line.Text = lines[currentPage];
                HighlightSpeaker(line.Text);
                var pageSpeaker = StoryLineSpeaker(story, line.Text);
                if (!diplomacy && pageSpeaker is { } general)
                {
                    portrait.Texture = PortraitFor(general) ?? CircularPortraitFor(general);
                    portraitName.Text = StorySpeakerName(general);
                }
            }
            if (currentPage >= lines.Length - 1)
            {
                next.Visible = false;
                if (replay) { var done = MakeButton("닫기"); done.Pressed += CloseStoryModal; actions.AddChild(done); }
                else AddStoryChoices(actions, story, line);
            }
        };
        actions.AddChild(next);
        if (lines.Length == 1)
        {
            next.Visible = false;
            if (replay) { var done = MakeButton("닫기"); done.Pressed += CloseStoryModal; actions.AddChild(done); }
            else AddStoryChoices(actions, story, line);
        }
    }

    private void CloseStoryModal()
    {
        if (!_storyOpen) return;
        _storyOpen = false;
        if (_storyOpenId is { } id) _state = GeneralStorySelector.Resolve(_state, id, null);
        _storyOpenId = null;
        if (_modalLayer is not null) { _modalLayer.QueueFree(); _modalLayer = null; }
        if (IsInstanceValid(_reportPanel)) _reportPanel.Visible = _storyReportWasVisible;
        _storyAwaitingCard = NextPendingStory() is not null;
    }

    private void AddStoryChoices(HBoxContainer actions, GeneralStoryEvent story, Label line)
    {
        if (story.Kind is "low_food" or "beast_defeated" or "scout_success" or "scout_fail"
            or "stratagem_success" or "stratagem_fail" or "alliance_success" or "alliance_fail" or "alliance_break"
            or "alliance_success_advisor" or "alliance_fail_advisor")
        {
            var acknowledge = MakeButton("확인", accent: true);
            acknowledge.Pressed += () =>
            {
                _state = GeneralStorySelector.Resolve(_state, story.Id, "acknowledged");
                CloseStoryModal();
            };
            actions.AddChild(acknowledge);
            return;
        }
        var definition = _storyDefinitions.FirstOrDefault(d => d.Kind == story.Kind);
        foreach (var (choice, label) in new[]
            { ("a", definition?.OptionA ?? "확인"), ("b", definition?.OptionB ?? "넘어가기") })
        {
            var button = MakeButton(label, choice == "a");
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            button.Pressed += () =>
            {
                var result = GeneralStoryChoices.Apply(_state, story.Id, choice, _balance);
                if (!result.Ok) { line.Text = result.Message; return; }
                _state = result.State;
                Report($"[장수 대화] {StoryTitle(story)} · {result.Message}", Gold,
                    story.Speaker, story.Id);
                line.Text = result.Message;
                foreach (var child in actions.GetChildren()) child.QueueFree();
                actions.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
                var done = MakeButton("확인", accent: true);
                done.Pressed += () =>
                {
                    CloseStoryModal();
                    Redraw(result.Message);
                    if (choice == "a") OpenStoryRelatedScreen(story);
                };
                actions.AddChild(done);
            };
            actions.AddChild(button);
        }
    }

    private void OpenStoryRelatedScreen(GeneralStoryEvent story)
    {
        switch (story.Kind)
        {
            case "beast_appeared":
                OpenTreasureList();
                break;
            case "research":
                if (story.City is { } city && _state.Cities.Any(c => c.Id == city && c.Owner == Player))
                {
                    SelectCity(city);
                    var lane = FactionResearch.IsGeneralResearch(story.Code) ? "general"
                        : FactionResearch.IsStratagemResearch(story.Code) ? "stratagem_research" : "troop";
                    var index = System.Array.FindIndex(Cmds, c => c.Kind == CommandKind.Research && c.Param == lane);
                    if (index >= 0) OpenModal(index);
                }
                break;
        }
    }

    private void ConnectStoryReportLabel(Label label, string storyId)
    {
        label.SetMeta("story_replay_id", storyId);
        label.MouseFilter = Control.MouseFilterEnum.Stop;
        label.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        label.MouseEntered += () => label.Modulate = GoldBright;
        label.MouseExited += () => label.Modulate = Colors.White;
        label.GuiInput += input =>
        {
            if (input is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) return;
            OpenStoryReplay(storyId);
        };
    }

    private void OpenStoryReplay(string storyId)
    {
        var saved = _state.StoryHistory.FirstOrDefault(story => story.Id == storyId && story.Faction == Player);
        if (saved is null) return;
        if (_modalLayer is not null) CloseModal();
        ShowStoryModal(saved, true);
    }

    private string StorySpeakerName(GeneralId id)
        => _state.Generals.FirstOrDefault(g => g.Id == id)?.Name ?? "장수";

    private string StoryTitle(GeneralStoryEvent story)
        => _storyDefinitions.FirstOrDefault(d => d.Kind == story.Kind)?.Title ?? "전장 소식";

    private string StoryShortLine(GeneralStoryEvent story)
        => StoryLines(story)[0].Replace($"{StorySpeakerName(story.Speaker)}: ", "");

    private GeneralId? StoryLineSpeaker(GeneralStoryEvent story, string line)
    {
        if (story.SecondSpeaker is { } ruler && line.StartsWith($"{StorySpeakerName(ruler)}:", System.StringComparison.Ordinal))
            return ruler;
        if (story.Kind.EndsWith("_advisor", System.StringComparison.Ordinal)
            && story.Amount > 0 && _state.Generals.Any(general => general.Id.Value == story.Amount)
            && line.StartsWith($"{StorySpeakerName(new GeneralId(story.Amount))}:", System.StringComparison.Ordinal))
            return new GeneralId(story.Amount);
        return line.StartsWith($"{StorySpeakerName(story.Speaker)}:", System.StringComparison.Ordinal)
            ? story.Speaker : null;
    }

    private string[] StoryLines(GeneralStoryEvent story)
    {
        if (story.Kind == "stratagem_success")
        {
            var effect = story.Code switch
            {
                "wall_break" => $"성벽 내구도 {story.Amount:N0} 감소",
                "incite" => $"치안 {story.Amount:N0} 감소",
                "arson" => $"군량 {story.Amount:N0} 피해",
                "steal" => $"금 {story.Amount:N0} 탈취",
                _ => "목표에 효과 적용",
            };
            return [$"{StorySpeakerName(story.Speaker)}: 계략에 성공했습니다. {effect}."];
        }
        var definition = _storyDefinitions.FirstOrDefault(d => d.Kind == story.Kind);
        if (definition is null) return [$"{StorySpeakerName(story.Speaker)}: 새로운 소식이 있습니다.", "보고에 기록했습니다."];
        var city = story.City is { } cityId
            ? _state.Cities.FirstOrDefault(c => c.Id == cityId)?.Name ?? "도시" : "도시";
        var second = story.SecondSpeaker is { } secondId ? StorySpeakerName(secondId) : StorySpeakerName(story.Speaker);
        var ruler = story.SecondSpeaker is { } rulerId ? StorySpeakerName(rulerId) : "상대 군주";
        var advisor = story.Amount > 0 ? StorySpeakerName(new GeneralId(story.Amount)) : "상대 참모";
        var result = definition.Variants[story.Variant % definition.Variants.Count]
            .Select(line => line.Replace("{speaker}", StorySpeakerName(story.Speaker))
                .Replace("{second}", second).Replace("{ruler}", ruler).Replace("{advisor}", advisor)
                .Replace("{city}", city)
                .Replace("{amount}", story.Amount.ToString("N0"))).ToList();
        if (definition.VoiceLines is not null
            && definition.VoiceLines.TryGetValue(story.Speaker.Value, out var voice)
            && result.Count < 4) result.Add($"{StorySpeakerName(story.Speaker)}: {voice}");
        return result.ToArray();
    }

    private async void RunGeneralStoryQa()
    {
        try
        {
            var speaker = _state.Generals.FirstOrDefault(g => _state.Assignments.Any(p => p.General == g.Id && p.Faction == Player));
            var city = _state.Cities.FirstOrDefault(c => c.Owner == Player);
            if (speaker is null || city is null) throw new System.InvalidOperationException("플레이어 장수/도시 없음");
            var story = new GeneralStoryEvent("qa:story", "capture", _state.Day, 1,
                Player, speaker.Id, null, city.Id, 0);
            ShowStorySpeech(story);
            var speechTracksWorld = _storySpeechPanel is null;
            var speechPortrait = _storySpeechPanel?.FindChild("StorySpeechPortrait", true, false) is TextureRect speechImage
                && speechImage.Texture is not null && speechImage.CustomMinimumSize.X == 30f;
            if (_storySpeechPanel is not null)
            {
                _storySpeechPanel.Position = Vector2.Zero;
                UpdateStorySpeechPosition();
                speechTracksWorld = _storySpeechPanel.Position != Vector2.Zero;
                _storySpeechLayer?.QueueFree();
                _storySpeechLayer = null;
                _storySpeechPanel = null;
                _storySpeechAnchor = null;
            }
            _state = _state with { PendingStoryEvents = _state.StoryPending.Append(story).ToArray() };
            var reportWasVisible = _reportPanel.Visible;
            ShowStoryModal(story, false);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var panel = _modalLayer?.GetChildren().OfType<PanelContainer>().FirstOrDefault();
            var row = panel?.FindChild("StoryDialogueRow", true, false) as HBoxContainer;
            var ratioOk = row is not null && row.GetChildCount() == 2
                && row.GetChild<Control>(0).Size.X < row.GetChild<Control>(1).Size.X
                && panel?.FindChildren("*", "Label", true, false).OfType<Label>()
                    .Any(label => label.Text == StoryTitle(story)
                        && label.GlobalPosition.Y + label.Size.Y <= row.GlobalPosition.Y) == true;
            var portrait = row?.GetChild<Control>(0).GetChildren().OfType<TextureRect>().FirstOrDefault();
            var portraitFitOk = portrait is not null
                && portrait.StretchMode == TextureRect.StretchModeEnum.KeepAspectCentered
                && portrait.Size.Y > 0 && portrait.Size.X > 0;
            var nextButton = panel?.FindChildren("*", "Button", true, false).OfType<Button>()
                .FirstOrDefault(b => b.Text == "다음 ▶");
            var nextAligned = nextButton is not null && panel is not null
                && nextButton.GlobalPosition.X > panel.GlobalPosition.X + panel.Size.X * 0.7f
                && nextButton.GlobalPosition.Y > panel.GlobalPosition.Y + panel.Size.Y * 0.5f;
            var hidden = !_reportPanel.Visible && _storyOpen;
            CloseAnyModalOrPanel();
            var restored = !_storyOpen && _modalLayer is null && _reportPanel.Visible == reportWasVisible
                && _state.StoryPending.All(e => e.Id != story.Id)
                && _state.StoryHistory.Any(e => e.Id == story.Id);
            ShowStoryModal(story, true);
            var replayReadOnly = _storyOpenId is null;
            CloseStoryModal();
            var choiceStory = story with { Id = "qa:choice" };
            _state = _state with { PendingStoryEvents = _state.StoryPending.Append(choiceStory).ToArray() };
            ShowStoryModal(choiceStory, false);
            var next = _modalLayer?.FindChildren("*", "Button", true, false).OfType<Button>()
                .FirstOrDefault(b => b.Text == "다음 ▶");
            next?.EmitSignal(Button.SignalName.Pressed);
            var choose = _modalLayer?.FindChildren("*", "Button", true, false).OfType<Button>()
                .FirstOrDefault(b => b.Text == "재정 보존");
            var choicesVisible = choose is not null && _storyOpen;
            choose?.EmitSignal(Button.SignalName.Pressed);
            var done = _modalLayer?.FindChildren("*", "Button", true, false).OfType<Button>()
                .FirstOrDefault(b => b.Text == "확인");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var choicePanel = _modalLayer?.GetChildren().OfType<PanelContainer>().FirstOrDefault();
            var doneAligned = done is not null && choicePanel is not null
                && done.GlobalPosition.X > choicePanel.GlobalPosition.X + choicePanel.Size.X * 0.7f
                && done.GlobalPosition.Y > choicePanel.GlobalPosition.Y + choicePanel.Size.Y * 0.5f;
            done?.EmitSignal(Button.SignalName.Pressed);
            var choiceResolved = !_storyOpen && _state.StoryHistory.Any(e => e.Id == choiceStory.Id && e.Choice == "b");
            OpenFullLog();
            var logLink = _modalLayer?.FindChildren("*", "Label", true, false).OfType<Label>()
                .Any(label => label.HasMeta("story_replay_id")
                    && label.GetMeta("story_replay_id").AsString() == choiceStory.Id) == true;
            OpenStoryReplay(choiceStory.Id);
            var reportReplay = _storyOpen && _storyOpenId is null;
            CloseStoryModal();
            var warningStory = story with { Id = "qa:low-food", Kind = "low_food" };
            _state = _state with { PendingStoryEvents = _state.StoryPending.Append(warningStory).ToArray() };
            ShowStoryModal(warningStory, false);
            _modalLayer?.FindChildren("*", "Button", true, false).OfType<Button>()
                .FirstOrDefault(button => button.Text == "다음 ▶")?.EmitSignal(Button.SignalName.Pressed);
            var warningButtons = _modalLayer?.FindChildren("*", "Button", true, false)
                .OfType<Button>().Select(button => button.Text).ToArray() ?? [];
            var warningOnly = warningButtons.Contains("확인") && !warningButtons.Contains("보급 준비")
                && !warningButtons.Contains("행군 유지");
            _modalLayer?.FindChildren("*", "Button", true, false).OfType<Button>()
                .FirstOrDefault(button => button.Text == "확인")?.EmitSignal(Button.SignalName.Pressed);
            warningOnly &= !_storyOpen && _state.StoryHistory.Any(e => e.Id == warningStory.Id);
            var allianceStory = story with { Id = "qa:alliance-result", Kind = "alliance_success" };
            _state = _state with { PendingStoryEvents = _state.StoryPending.Append(allianceStory).ToArray() };
            ShowStoryModal(allianceStory, false);
            for (var page = 1; page < StoryLines(allianceStory).Length; page++)
                _modalLayer?.FindChildren("*", "Button", true, false).OfType<Button>()
                    .FirstOrDefault(button => button.Text == "다음 ▶")?.EmitSignal(Button.SignalName.Pressed);
            var allianceButtons = _modalLayer?.FindChildren("*", "Button", true, false)
                .OfType<Button>().Select(button => button.Text).ToArray() ?? [];
            var allianceResultShown = allianceButtons.Contains("확인")
                && !allianceButtons.Contains("결과 확인") && StoryLines(allianceStory).Any(text => text.Contains("동맹", System.StringComparison.Ordinal));
            _modalLayer?.FindChildren("*", "Button", true, false).OfType<Button>()
                .FirstOrDefault(button => button.Text == "확인")?.EmitSignal(Button.SignalName.Pressed);
            allianceResultShown &= !_storyOpen && _state.StoryHistory.Any(e => e.Id == allianceStory.Id);
            var otherSpeakers = _state.Generals.Where(general => general.Id != speaker.Id).Take(2).ToArray();
            var audienceShown = false;
            if (otherSpeakers.Length == 2)
            {
                var audienceStory = story with
                {
                    Id = "qa:alliance-audience", Kind = "alliance_success_advisor",
                    SecondSpeaker = otherSpeakers[0].Id, Amount = otherSpeakers[1].Id.Value,
                };
                _state = _state with { PendingStoryEvents = _state.StoryPending.Append(audienceStory).ToArray() };
                ShowStoryModal(audienceStory, false);
                var audienceLines = StoryLines(audienceStory);
                var ownPortrait = _modalLayer?.FindChild("StoryCurrentSpeaker", true, false)?.GetParent<Control>();
                var ownPage = ownPortrait?.Visible == true;
                _modalLayer?.FindChildren("*", "Button", true, false).OfType<Button>()
                    .FirstOrDefault(button => button.Text == "다음 ▶")?.EmitSignal(Button.SignalName.Pressed);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var rulerImage = _modalLayer?.FindChild("DiplomacyRulerPortrait", true, false) as TextureRect;
                var advisorImage = _modalLayer?.FindChild("DiplomacyAdvisorPortrait", true, false) as TextureRect;
                var dialogueRow = _modalLayer?.FindChild("StoryDialogueRow", true, false) as HBoxContainer;
                audienceShown = audienceLines.Length == 4
                    && audienceLines.Any(text => text.StartsWith($"{otherSpeakers[0].Name}:"))
                    && audienceLines.Any(text => text.StartsWith($"{otherSpeakers[1].Name}:"))
                    && rulerImage?.Texture is not null && advisorImage?.Texture is not null
                    && ownPage && ownPortrait?.Visible == false && advisorImage.Visible
                    && dialogueRow is not null
                    && advisorImage.Size.Y >= dialogueRow.Size.Y * 0.55f
                    && advisorImage.GlobalPosition.Y >= dialogueRow.GlobalPosition.Y;
                CloseStoryModal();
            }
            var defeatedStory = story with { Id = "qa:beast-defeated", Kind = "beast_defeated" };
            _state = _state with { PendingStoryEvents = _state.StoryPending.Append(defeatedStory).ToArray() };
            ShowStoryModal(defeatedStory, false);
            _modalLayer?.FindChildren("*", "Button", true, false).OfType<Button>()
                .FirstOrDefault(button => button.Text == "다음 ▶")?.EmitSignal(Button.SignalName.Pressed);
            var beastButtons = _modalLayer?.FindChildren("*", "Button", true, false)
                .OfType<Button>().Select(button => button.Text).ToArray() ?? [];
            var beastConfirmOnly = beastButtons.Contains("확인") && !beastButtons.Contains("보상 내역 보기");
            _modalLayer?.FindChildren("*", "Button", true, false).OfType<Button>()
                .FirstOrDefault(button => button.Text == "확인")?.EmitSignal(Button.SignalName.Pressed);
            beastConfirmOnly &= !_storyOpen && _state.StoryHistory.Any(e => e.Id == defeatedStory.Id);
            var unrelatedStory = story with { Id = "qa:other-result", Kind = "wall" };
            var priorityAlliance = story with { Id = "qa:priority-alliance", Kind = "alliance_success" };
            var priorityStratagem = story with { Id = "qa:priority-stratagem", Kind = "stratagem_success" };
            _state = _state with { PendingStoryEvents = _state.StoryPending.Append(unrelatedStory)
                .Append(priorityStratagem).Append(priorityAlliance).ToArray() };
            var alliancePriority = NextPendingStory()?.Id == priorityAlliance.Id;
            _state = GeneralStorySelector.Resolve(_state, priorityAlliance.Id, "acknowledged");
            var stratagemPriority = NextPendingStory()?.Id == priorityStratagem.Id;
            _state = GeneralStorySelector.Resolve(_state, priorityStratagem.Id, "acknowledged");
            var queuedNext = NextPendingStory()?.Id == unrelatedStory.Id;
            _state = GeneralStorySelector.Resolve(_state, unrelatedStory.Id, "acknowledged");
            var passed = speechTracksWorld && speechPortrait && ratioOk && portraitFitOk && nextAligned && doneAligned && hidden && restored && replayReadOnly && choicesVisible && choiceResolved && logLink && reportReplay && warningOnly && beastConfirmOnly && allianceResultShown && audienceShown && alliancePriority && stratagemPriority && queuedNext;
            GD.Print($"[general-story-qa] passed={passed} alliancePriority={alliancePriority} stratagemPriority={stratagemPriority} queuedNext={queuedNext} speechTracksWorld={speechTracksWorld} speechPortrait={speechPortrait} ratio={ratioOk} portraitFit={portraitFitOk} nextAligned={nextAligned} doneAligned={doneAligned} reportHidden={hidden} restored={restored} replayReadOnly={replayReadOnly} choices={choicesVisible} resolved={choiceResolved} logLink={logLink} reportReplay={reportReplay} warningOnly={warningOnly} beastConfirmOnly={beastConfirmOnly} allianceResultShown={allianceResultShown} audienceShown={audienceShown}");
            GetTree().Quit(passed ? 0 : 1);
        }
        catch (System.Exception error)
        {
            GD.PrintErr($"[general-story-qa] failed: {error}");
            GetTree().Quit(1);
        }
    }
}
