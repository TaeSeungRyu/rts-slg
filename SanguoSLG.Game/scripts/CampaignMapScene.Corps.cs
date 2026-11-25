namespace SanguoSLG.Game;

using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

public sealed partial class CampaignMapScene
{
    private async void RunSystemAfterCorpsQa()
    {
        OpenSystemPalette();
        var guideButton = _modalLayer?.FindChildren("*", "Button", true, false).OfType<Button>()
            .FirstOrDefault(button => button.Text == "부대설명");
        guideButton?.EmitSignal(BaseButton.SignalName.Pressed);
        var guideBack = _modalLayer?.FindChild("SystemViewBack", true, false) as Button;
        guideBack?.EmitSignal(BaseButton.SignalName.ButtonDown);
        var corpsButton = _modalLayer?.FindChildren("*", "Button", true, false).OfType<Button>()
            .FirstOrDefault(button => button.Text == "군단편성");
        corpsButton?.EmitSignal(BaseButton.SignalName.Pressed);
        var back = _modalLayer?.FindChild("SystemViewBack", true, false) as Button;
        back?.EmitSignal(BaseButton.SignalName.ButtonDown);
        var close = _modalLayer?.FindChild("SystemPaletteClose", true, false) as Button;
        close?.EmitSignal(BaseButton.SignalName.ButtonDown);
        await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
        var leftovers = GetChildren().OfType<CanvasLayer>().Count(layer =>
            layer.GetChildren().OfType<PanelContainer>().Any(panel => panel.HasMeta("modal_root")));
        OpenSystemPalette();
        OpenArmyCorpsList();
        (_modalLayer?.FindChild("SystemViewBack", true, false) as Button)?
            .EmitSignal(BaseButton.SignalName.ButtonDown);
        var backdrop = _modalLayer?.FindChild("ModalBackdrop", true, false) as ColorRect;
        backdrop?.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left, Pressed = true,
        });
        await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
        var backdropLeftovers = GetChildren().OfType<CanvasLayer>().Count(layer =>
            layer.GetChildren().OfType<PanelContainer>().Any(panel => panel.HasMeta("modal_root")));
        var passed = guideButton is not null && guideBack is not null
            && corpsButton is not null && back is not null && close is not null
            && backdrop is not null && _modalLayer is null && leftovers == 0
            && backdropLeftovers == 0;
        GD.Print($"[maptestsystemaftercorpsqa] back={back is not null} close={close is not null} closeLeftovers={leftovers} backdropLeftovers={backdropLeftovers} passed={passed}");
        GetTree().Quit(passed ? 0 : 1);
    }

    private async void RunArmyCorpsModalQa()
    {
        OpenSystemPalette();
        OpenArmyCorpsList();
        var cards = _modalLayer?.FindChild("ArmyCorpsCards", true, false) as GridContainer;
        var cardsValid = cards is not null && cards.Columns == 2;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var listPanel = _modalLayer?.GetChildren().OfType<PanelContainer>()
            .FirstOrDefault(candidate => candidate.HasMeta("modal_root"));
        var viewport = GetViewport().GetVisibleRect().Size;
        var centered = listPanel is not null
            && Mathf.Abs(listPanel.Position.Y + listPanel.Size.Y * 0.5f - viewport.Y * 0.5f) <= 2f;
        OpenArmyCorpsEditor(null);
        var cityGrid = _modalLayer?.FindChild("ArmyCorpsCityGrid", true, false) as GridContainer;
        var commanderPreview = _modalLayer?.FindChild("ArmyCorpsCommanderPreview", true, false) as HBoxContainer;
        var policy = _modalLayer?.FindChild("ArmyCorpsCombatPolicy", true, false) as Button;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var editorPanel = _modalLayer?.GetChildren().OfType<PanelContainer>()
            .FirstOrDefault(candidate => candidate.HasMeta("modal_root"));
        var editorValid = cityGrid is not null && commanderPreview is not null && policy is not null
            && editorPanel is ModalChrome
            && Mathf.Abs(editorPanel.Position.Y + editorPanel.Size.Y * 0.5f - viewport.Y * 0.5f) <= 2f
            && editorPanel.Position.Y + editorPanel.Size.Y <= viewport.Y;
        OpenArmyCorpsList();
        var close = _modalLayer?.FindChild("SystemViewClose", true, false) as Button;
        close?.EmitSignal(BaseButton.SignalName.ButtonDown);
        await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
        var leftovers = GetChildren().OfType<CanvasLayer>().Count(layer =>
            layer.GetChildren().OfType<PanelContainer>().Any(panel => panel.HasMeta("modal_root")));
        var passed = cardsValid && centered && editorValid && close is not null
            && _modalLayer is null && leftovers == 0;
        GD.Print($"[maptestcorpsmodalqa] cards={cardsValid} centered={centered} editor={editorValid} close={close is not null} leftovers={leftovers} passed={passed}");
        GetTree().Quit(passed ? 0 : 1);
    }

    private void OpenArmyCorpsList()
    {
        if (_modalLayer is not null) { FadeOutModal(_modalLayer); _modalLayer = null; }
        var width = Mathf.Min(940f, GetViewport().GetVisibleRect().Size.X * 0.9f);
        var height = Mathf.Min(640f, GetViewport().GetVisibleRect().Size.Y * 0.86f);
        var box = SystemView("군단편성", width, out var scroll, out var panel, out var titleRow,
            useModalChrome: true);
        var owned = _state.Cities.Count(city => city.Owner == Player);
        var delegated = _state.Corps.Where(entry => entry.Faction == Player)
            .SelectMany(entry => entry.Cities).Distinct().Count();


        // var overviewFrame = CorpsPanel(10, out var overview);
        // box.AddChild(overviewFrame);
        // var overviewRow = new HBoxContainer();
        // overview.AddChild(overviewRow);
var overviewFrame = CorpsPanel(10, out var overview);

// 상단 여백 추가
var overviewMargin = new MarginContainer
{
    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
};

overviewMargin.AddThemeConstantOverride("margin_top", 10);

box.AddChild(overviewMargin);
overviewMargin.AddChild(overviewFrame);

var overviewRow = new HBoxContainer();
overview.AddChild(overviewRow);



        var overviewText = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        overviewRow.AddChild(overviewText);
        overviewText.AddChild(MakeLabel("군단 운영", 20, GoldBright));
        overviewText.AddChild(MakeLabel($"위임 거점 {delegated}/{owned}  ·  활동 군단 {_state.Corps.Count(entry => entry.Faction == Player)}", 13, Parchment));
        var create = MakeButton("＋ 군단 편성", accent: true);
        create.CustomMinimumSize = new Vector2(145, 42);
        create.Pressed += () => OpenArmyCorpsEditor(null);
        overviewRow.AddChild(create);
        box.AddChild(MakeLabel("군단장은 지정된 거점의 장수·병력·자원을 운용합니다.", 13, Parchment));
        var cards = new GridContainer { Name = "ArmyCorpsCards", Columns = 2,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        cards.AddThemeConstantOverride("h_separation", 12);
        cards.AddThemeConstantOverride("v_separation", 12);
        box.AddChild(cards);
        foreach (var corps in _state.Corps.Where(entry => entry.Faction == Player).OrderBy(entry => entry.Id))
        {
            var cardFrame = CorpsPanel(12, out var card);
            cardFrame.CustomMinimumSize = new Vector2(410, 164);
            cards.AddChild(cardFrame);
            var heading = new HBoxContainer();
            card.AddChild(heading);
            var number = MakeLabel($"제{corps.Id}군단", 17, GoldBright);
            number.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            heading.AddChild(number);
            heading.AddChild(MakeLabel(corps.Policy == ArmyCorpsPolicy.Combat ? "⚔ 전투중심" : "◆ 내정중심", 13, Gold));
            var commander = _state.Generals.FirstOrDefault(general => general.Id == corps.Commander);
            var leaderRow = new HBoxContainer();
            card.AddChild(leaderRow);
            leaderRow.AddChild(new TextureRect { Texture = commander is null ? null : CircularPortraitFor(commander.Id),
                CustomMinimumSize = new Vector2(54, 54), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
            var leaderText = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            leaderRow.AddChild(leaderText);
            leaderText.AddChild(MakeLabel(commander?.Name ?? "군단장 없음", 16, Parchment));
            leaderText.AddChild(MakeLabel($"담당 거점 {corps.Cities.Count}곳", 12, Gold));
            var names = corps.Cities.Select(id => _state.Cities.FirstOrDefault(city => city.Id == id)?.Name ?? "상실").ToList();
            card.AddChild(MakeLabel(names.Count <= 4 ? string.Join(" · ", names)
                : string.Join(" · ", names.Take(4)) + $" 외 {names.Count - 4}곳", 13, Parchment));
            var row = new HBoxContainer();
            card.AddChild(row);
            var edit = MakeButton("편집");
            edit.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            edit.Pressed += () => OpenArmyCorpsEditor(corps.Id);
            row.AddChild(edit);
            var disband = MakeButton("해산");
            disband.Pressed += () =>
            {
                _state = ArmyCorpsService.Disband(_state, corps.Id, Player);
                OpenArmyCorpsList();
            };
            row.AddChild(disband);
        }
        if (cards.GetChildCount() == 0)
        {
            var emptyFrame = CorpsPanel(24, out var empty);
            box.AddChild(emptyFrame);
            empty.AddChild(MakeLabel("아직 편성한 군단이 없습니다.", 18, GoldBright));
            empty.AddChild(MakeLabel("위의 ‘군단 편성’을 눌러 군단장과 담당 거점을 지정하세요.", 13, Parchment));
        }
        // 높이는 PNG 헤더/푸터를 포함한 외곽 패널에 지정한다. 본문 스크롤에
        // 전체 높이를 넣으면 장식 높이가 추가되어 화면 아래로 밀려난다.
        panel.CustomMinimumSize = new Vector2(width, height);
        CenterAndDrag(panel, titleRow, width, height, box);
        CenterCorpsPanelAfterLayout(panel, width, height);
    }

    private void CenterCorpsPanelAfterLayout(PanelContainer panel, float width, float height)
    {
        // CustomMinimumSize만 바꾼 직후에는 Container의 실제 Size가 아직 0이거나
        // 이전 화면의 값일 수 있다. 이번 프레임에서 먼저 목표 폭·높이를 적용한다.
        panel.Size = new Vector2(width, height);
        void Center()
        {
            if (!GodotObject.IsInstanceValid(panel) || !panel.IsInsideTree()
                || panel.IsQueuedForDeletion()) return;
            // 자식 레이아웃이 확정된 다음 실제 외곽 Size로 중심을 맞춘다.
            // 가장자리 8px은 화면 경계 안전 여백이다. 위치를 미세 조정하려면
            // 이 식의 X/Y에 원하는 픽셀 오프셋을 더하면 된다.
            var viewport = GetViewport().GetVisibleRect().Size;
            panel.Position = new Vector2(
                Mathf.Max(8f, (viewport.X - panel.Size.X) * 0.5f),
                Mathf.Max(8f, (viewport.Y - panel.Size.Y) * 0.5f));
        }
        Center();
        Callable.From(Center).CallDeferred();
    }

    private PanelContainer CorpsPanel(int padding, out VBoxContainer contents)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", Frame(InkSoft, new Color(Gold, 0.55f), 1, 9, padding));
        contents = new VBoxContainer();
        contents.AddThemeConstantOverride("separation", 8);
        panel.AddChild(contents);
        return panel;
    }

    private void OpenArmyCorpsEditor(int? corpsId)
    {
        if (_modalLayer is not null) { FadeOutModal(_modalLayer); _modalLayer = null; }
        var existing = _state.Corps.FirstOrDefault(entry => entry.Id == corpsId && entry.Faction == Player);
        // 외곽 PNG 프레임을 포함한 목표 크기. X는 좌우 콘텐츠 공간,
        // Y는 헤더·본문·푸터의 전체 높이이며 뷰포트 비율이 상한이다.
        var width = Mathf.Min(1040f, GetViewport().GetVisibleRect().Size.X * 0.94f);
        var height = Mathf.Min(690f, GetViewport().GetVisibleRect().Size.Y * 0.88f);



        
        var box = SystemView(existing is null ? "새 군단 편성" : $"제{existing.Id}군단 편집", width,
            out var scroll, out var panel, out var titleRow, useModalChrome: true);
        scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
        box.AddChild(MakeLabel("\n담당 거점과 군단장을 고른 뒤 운영 방침을 정하세요.", 13, Parchment));
        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        // 왼쪽 거점 선택과 오른쪽 군단장/방침 사이의 가로 간격(px).
        body.AddThemeConstantOverride("separation", 14);
        box.AddChild(body);
        





        var leftFrame = CorpsPanel(14, out var left);
        leftFrame.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        body.AddChild(leftFrame);
        var cityHeading = new HBoxContainer();
        left.AddChild(cityHeading);
        var cityTitle = MakeLabel("담당 성·항구", 17, GoldBright);
        cityTitle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        cityHeading.AddChild(cityTitle);
        var countLabel = MakeLabel("0곳 선택", 13, Gold);
        cityHeading.AddChild(countLabel);
        left.AddChild(MakeLabel("선택한 거점의 장수와 자원만 군단이 사용합니다.", 12, Parchment));
        // 외곽 전체 높이에서 PNG 헤더·푸터와 상하 동작 버튼을 뺀 목록 높이.
        // 목록이 많을 때는 이 영역만 스크롤하고 모달 바깥 높이는 유지한다.
        var cityScroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, Mathf.Max(190f, height - 390f)),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        left.AddChild(cityScroll);
        var cityGrid = new GridContainer { Name = "ArmyCorpsCityGrid", Columns = 2,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        cityGrid.AddThemeConstantOverride("h_separation", 8);
        cityGrid.AddThemeConstantOverride("v_separation", 8);
        cityScroll.AddChild(cityGrid);
        var selected = existing?.Cities.ToHashSet() ?? [];
        var leaderOption = MakeOption(320);
        var commanderPortrait = new TextureRect { CustomMinimumSize = new Vector2(58, 58),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
        var commanderName = MakeLabel("군단장 선택", 16, Parchment);
        void UpdateCount() => countLabel.Text = $"{selected.Count}곳 선택";
        foreach (var city in _state.Cities.Where(city => city.Owner == Player).OrderBy(city => city.Name))
        {
            var occupied = _state.Corps.Any(other => other.Faction == Player
                && other.Id != corpsId && other.Cities.Contains(city.Id));
            var check = MakeButton($"{(selected.Contains(city.Id) ? "✓  " : "＋  ")}{city.Name}  ·  {(city.IsPort ? "항구" : "성")}");
            check.ToggleMode = true;
            check.ButtonPressed = selected.Contains(city.Id);
            check.Disabled = occupied;
            check.TooltipText = occupied ? "이미 다른 군단이 맡은 거점입니다." : "선택하여 군단에 배치";
            check.CustomMinimumSize = new Vector2(205, 48);
            check.AddThemeStyleboxOverride("hover", Frame(InkHover, GoldBright, 1, 7, 9));
            check.AddThemeStyleboxOverride("pressed", Frame(AccentFill, GoldBright, 1, 7, 9));
            check.Toggled += pressed =>
            {
                check.Text = $"{(pressed ? "✓  " : "＋  ")}{city.Name}  ·  {(city.IsPort ? "항구" : "성")}";
                if (pressed) selected.Add(city.Id);
                else selected.Remove(city.Id);
                UpdateCount();
                RefreshCommanders();
            };
            cityGrid.AddChild(check);
        }
        UpdateCount();

        var rightFrame = CorpsPanel(14, out var right);
        rightFrame.CustomMinimumSize = new Vector2(350, 0);
        body.AddChild(rightFrame);
        right.AddChild(MakeLabel("군단장", 17, GoldBright));
        var commanderPreview = new HBoxContainer { Name = "ArmyCorpsCommanderPreview",
            CustomMinimumSize = new Vector2(0, 64) };
        right.AddChild(commanderPreview);
        commanderPreview.AddChild(commanderPortrait);
        commanderPreview.AddChild(commanderName);
        right.AddChild(leaderOption);
        right.AddChild(GoldRule());
        right.AddChild(MakeLabel("군단방침", 17, GoldBright));
        var policy = existing?.Policy ?? ArmyCorpsPolicy.Domestic;
        var domesticButton = MakeButton("내정중심   ·   성장과 수비에 집중");
        domesticButton.CustomMinimumSize = new Vector2(0, 46);
        var combatButton = MakeButton("전투중심   ·   AI 공세 규칙 사용");
        combatButton.Name = "ArmyCorpsCombatPolicy";
        combatButton.CustomMinimumSize = new Vector2(0, 46);
        void SelectPolicy(ArmyCorpsPolicy value)
        {
            policy = value;
            domesticButton.AddThemeStyleboxOverride("normal", Frame(
                value == ArmyCorpsPolicy.Domestic ? AccentFill : InkSoft,
                value == ArmyCorpsPolicy.Domestic ? GoldBright : Gold, 1, 7, 9));
            combatButton.AddThemeStyleboxOverride("normal", Frame(
                value == ArmyCorpsPolicy.Combat ? AccentFill : InkSoft,
                value == ArmyCorpsPolicy.Combat ? GoldBright : Gold, 1, 7, 9));
        }
        domesticButton.Pressed += () => SelectPolicy(ArmyCorpsPolicy.Domestic);
        combatButton.Pressed += () => SelectPolicy(ArmyCorpsPolicy.Combat);
        right.AddChild(domesticButton);
        right.AddChild(combatButton);
        SelectPolicy(policy);
        right.AddChild(MakeLabel("내정중심은 신규 공세를 시작하지 않습니다.\n전투중심은 기존 AI의 공세·방어·지원 규칙을 따릅니다.", 12, Parchment));
        var error = MakeLabel("", 13, Gold);
        right.AddChild(error);

        void RefreshCommanders()
        {
            var former = leaderOption.Selected >= 0 ? leaderOption.GetItemId(leaderOption.Selected)
                : existing?.Commander.Value ?? -1;
            leaderOption.Clear();
            foreach (var general in _state.Generals.Where(general =>
                    _state.PostingOf(general.Id) is { } posting && posting.Faction == Player
                    && posting.Location is { } city && selected.Contains(city)
                    && !_state.IsGeneralInField(general.Id)
                    && !_state.Corps.Any(other => other.Id != corpsId && other.Commander == general.Id))
                .OrderBy(general => general.Name))
                leaderOption.AddItem(general.Name, general.Id.Value);
            for (var index = 0; index < leaderOption.ItemCount; index++)
                if (leaderOption.GetItemId(index) == former) { leaderOption.Select(index); break; }
            if (leaderOption.Selected < 0 && leaderOption.ItemCount > 0) leaderOption.Select(0);
            UpdateCommanderPreview();
        }
        void UpdateCommanderPreview()
        {
            var general = leaderOption.Selected < 0 ? null : _state.Generals.FirstOrDefault(entry =>
                entry.Id.Value == leaderOption.GetItemId(leaderOption.Selected));
            commanderPortrait.Texture = general is null ? null : CircularPortraitFor(general.Id);
            commanderName.Text = general?.Name ?? "군단장 선택";
        }
        leaderOption.ItemSelected += _ => UpdateCommanderPreview();
        RefreshCommanders();
     //   box.AddChild(GoldRule());
        var actions = new HBoxContainer();
        box.AddChild(actions);
        var spacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        actions.AddChild(spacer);
        var cancel = MakeButton("취소");
        cancel.CustomMinimumSize = new Vector2(100, 38);
        cancel.Pressed += OpenArmyCorpsList;
        actions.AddChild(cancel);
        var save = MakeButton("군단 저장", accent: true);
        save.CustomMinimumSize = new Vector2(125, 38);
        save.Pressed += () =>
        {
            if (leaderOption.Selected < 0)
            {
                error.Text = "맡길 거점과 주둔 군단장을 선택하세요.";
                return;
            }
            try
            {
                _state = ArmyCorpsService.Save(_state, corpsId, Player,
                    new GeneralId(leaderOption.GetItemId(leaderOption.Selected)), selected,
                    policy);
                OpenArmyCorpsList();
            }
            catch (System.InvalidOperationException exception) { error.Text = exception.Message; }
        };
        actions.AddChild(save);
        // BodyScroll에 전체 높이를 강제하면 PNG 장식 높이만큼 모달이 커진다.
        // 외곽 패널에만 폭/높이를 지정하고, 본문은 남은 공간을 사용한다.
        panel.CustomMinimumSize = new Vector2(width, height);
        CenterAndDrag(panel, titleRow, width, height, box);
        CenterCorpsPanelAfterLayout(panel, width, height);
    }
}
