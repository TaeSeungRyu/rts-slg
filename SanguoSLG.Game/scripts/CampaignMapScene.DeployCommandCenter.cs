using Godot;
using SanguoSLG.Core.Simulation;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private const string DeployCommandAssetRoot = "res://assets/ui/game/deploy_command/";
    private HBoxContainer? _deployComposeHeroCards;

    private void OpenDeployHub()
    {
        var cityId = _depModalCity;
        var city = _state.Cities.First(city => city.Id == cityId);
        if (_modalLayer is not null) { FadeOutModal(_modalLayer); _modalLayer = null; }

        var layer = new CanvasLayer { Layer = 20 };
        AddChild(layer);
        _modalLayer = layer;
        var backdrop = new ColorRect { Name = "ModalBackdrop", Color = new Color(0, 0, 0, 0.68f) };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        backdrop.MouseFilter = Control.MouseFilterEnum.Stop;
        layer.AddChild(backdrop);

        var viewport = GetViewport().GetVisibleRect().Size;
        var modalHeight = Mathf.Min(viewport.Y - 28f, 970f);
        var modalWidth = Mathf.Min(viewport.X - 28f, modalHeight * 1.5f);
        var panel = new PanelContainer
        {
            Name = "DeployCommandCenter",
            CustomMinimumSize = new Vector2(modalWidth, modalHeight),
            Size = new Vector2(modalWidth, modalHeight),
            Position = (viewport - new Vector2(modalWidth, modalHeight)) / 2f,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        panel.SetMeta("modal_root", true);
        layer.AddChild(panel);

        var background = new TextureRect
        {
            Name = "DeployCommandCenterBackground",
            Texture = GD.Load<Texture2D>(DeployCommandAssetRoot + "deploy_command_panel.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        background.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        panel.AddChild(background);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 45);
        margin.AddThemeConstantOverride("margin_right", 45);
        margin.AddThemeConstantOverride("margin_top", 13);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        panel.AddChild(margin);
        var root = new VBoxContainer { Name = "DeployCommandCenterContent" };
        root.AddThemeConstantOverride("separation", 0);
        margin.AddChild(root);
        var topRegion = new VBoxContainer
        {
            Name = "DeployCommandTopRegion",
            CustomMinimumSize = new Vector2(0, 88),
        };
        var middleRegion = new VBoxContainer
        {
            Name = "DeployCommandMiddleRegion",
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        middleRegion.AddThemeConstantOverride("separation", 8);
        var bottomRegion = new VBoxContainer
        {
            Name = "DeployCommandBottomRegion",
            CustomMinimumSize = new Vector2(0, 56),
        };
        bottomRegion.AddThemeConstantOverride("separation", 6);
        root.AddChild(topRegion);
        root.AddChild(middleRegion);
        root.AddChild(bottomRegion);

        var mine = Enumerable.Range(0, _pendingDeploys.Count)
            .Where(index => _pendingDeploys[index].Req.City == cityId).ToList();
        var queued = _state.Armies.Where(unit => unit.OriginCity == cityId && unit.IsWaitingDeployment
            && unit.Field.Owner == Player).OrderBy(unit => unit.Field.CommandOrder)
            .ThenBy(unit => unit.Id.Value).ToList();
        if (!mine.Contains(_depSelectedUnit)) _depSelectedUnit = mine.FirstOrDefault(-1);

        var header = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 76),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        header.AddThemeConstantOverride("separation", 10);
        header.AddChild(new Control { CustomMinimumSize = new Vector2(28, 0), MouseFilter = Control.MouseFilterEnum.Ignore });


        var title = MakeLabel($"출전 지휘소  《 {city.Name} 》", 27, GoldBright);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        title.VerticalAlignment = VerticalAlignment.Center;
        title.MouseFilter = Control.MouseFilterEnum.Ignore;

        // 2. 위로 이동: MarginContainer를 감싸고 margin_top에 음수(-8)를 주어 위로 올림
        var titleMargin = new MarginContainer 
        { 
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, 
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        titleMargin.AddThemeConstantOverride("margin_top", -15); // 값이 클수록(절대값 기준) 더 위로 올라갑니다 (-5, -10 등 조절)
        titleMargin.AddChild(title);
        
        header.AddChild(titleMargin);

        var close = MakeButton("×");
        close.Name = "DeployCommandClose";
        close.CustomMinimumSize = new Vector2(30, 30);
        close.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        close.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        close.AddThemeFontSizeOverride("font_size", 18);
        close.AddThemeStyleboxOverride("normal", Frame(Colors.Transparent, Gold, 1, 4, 2));
        close.AddThemeStyleboxOverride("hover", Frame(new Color("5b1712"), GoldBright, 1, 4, 2));
        close.AddThemeStyleboxOverride("pressed", Frame(new Color("35100d"), GoldBright, 1, 4, 2));
        close.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        close.Pressed += () => { CloseModal(); SelectCity(cityId); };
        var closeAlign = new MarginContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        closeAlign.AddThemeConstantOverride("margin_bottom", 8);
        closeAlign.AddChild(close);
        header.AddChild(closeAlign);
        topRegion.AddChild(header);

        var status = new HBoxContainer { CustomMinimumSize = new Vector2(0, 44) };
        status.AddThemeConstantOverride("separation", 18);
        status.AddChild(MakeLabel($"예약된 부대  {mine.Count}", 17, GoldBright));
        status.AddChild(MakeLabel("│", 17, Gold));
        status.AddChild(MakeLabel($"출격 대기  {queued.Count}", 17, GoldBright));
        var statusSpacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        status.AddChild(statusSpacer);
        var add = CommandCenterButton("＋ 신규 편성", true, new Vector2(170, 42));
        add.Name = "DeployCommandNewUnit";
        add.Pressed += () => OpenDeployCompose(-1);
        var addOffset = new MarginContainer();
        addOffset.AddThemeConstantOverride("margin_top", 14);
        addOffset.AddChild(add);
        status.AddChild(addOffset);
        middleRegion.AddChild(status);

        var content = new HBoxContainer
        {
            Name = "DeployCommandCenterColumns",
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        content.AddThemeConstantOverride("separation", 10);
        middleRegion.AddChild(content);

        var left = CommandCenterSection("예약 부대 목록");
        left.CustomMinimumSize = new Vector2(405, 0);
        left.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        content.AddChild(left);
        var listScroll = new ScrollContainer
        {
            Name = "DeployCommandUnitList",
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        GameUiSkin.ApplyQuickListScroll(listScroll);
        left.AddChild(listScroll);
        var list = new VBoxContainer 
        { 
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            // 부대가 없을 때를 대비해 기본적으로 세로 공간을 채우고 중앙 정렬을 준비합니다
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            Alignment = mine.Count == 0 ? BoxContainer.AlignmentMode.Center : BoxContainer.AlignmentMode.Begin
        };
        list.AddThemeConstantOverride("separation", 8);
        listScroll.AddChild(list);

        foreach (var index in mine) list.AddChild(BuildDeployCommandCard(index));

        if (mine.Count == 0)
        {
            var empty = MakeLabel("예약된 부대가 없습니다.\n신규 편성으로 부대를 추가하세요.", 15, Parchment);
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.VerticalAlignment = VerticalAlignment.Center;
            list.AddChild(empty);
        }

        // --- 👇 [여기에 세로 구분선 추가] ---
        // 1. 세로선을 감쌀 마진 컨테이너 생성
        var separatorMargin = new MarginContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        // 위아래로 줄이고 싶은 만큼 수치 조절 (예: 각각 15픽셀씩 안쪽으로 줄이기)
        //separatorMargin.AddThemeConstantOverride("margin_top", 15);
        separatorMargin.AddThemeConstantOverride("margin_bottom", 36);

        // 2. 기존 세로선 생성
        var separator = new Panel
        {
            CustomMinimumSize = new Vector2(1, 0),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        separator.AddThemeStyleboxOverride("panel", Frame(Gold, Colors.Transparent, 0, 0, 0));

        // 3. 마진 컨테이너에 선을 넣고, content에 추가
        separatorMargin.AddChild(separator);
        content.AddChild(separatorMargin);
        // ---------------------------------        

        var right = CommandCenterSection("선택 부대 정보");
        right.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        right.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        content.AddChild(right);
        BuildDeployCommandDetails(right, cityId);

        // var summary = new PanelContainer { CustomMinimumSize = new Vector2(0, 50) };
        // summary.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 4, 10));
        // summary.AddChild(MakeLabel(DeployCommandSummary(), 14, GoldBright));
        // bottomRegion.AddChild(summary);

        var actions = new HBoxContainer { CustomMinimumSize = new Vector2(0, 46) };
        actions.AddThemeConstantOverride("separation", 8);
        if (_depSelectedUnit >= 0 && _depSelectedUnit < _pendingDeploys.Count)
        {
            var selected = _depSelectedUnit;
            var remove = CommandCenterButton("부대 삭제", false, new Vector2(132, 42));
            remove.Pressed += () =>
            {
                _pendingDeploys.RemoveAt(selected);
                _depSelectedUnit = -1;
                SelectCity(cityId);
                OpenDeployHub();
            };
            actions.AddChild(remove);
            var edit = CommandCenterButton("편성 수정", false, new Vector2(145, 42));
            edit.Pressed += () => OpenDeployCompose(selected);
            actions.AddChild(edit);
        }
        actions.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var done = CommandCenterButton("확인", true, new Vector2(190, 42));
        done.Pressed += () => { CloseModal(); SelectCity(cityId); };
        actions.AddChild(done);
        
        // bottomRegion에는 actions만 추가
        bottomRegion.AddChild(actions);

        CenterAndDrag(panel, header, modalWidth, modalHeight, root);
        panel.Size = new Vector2(modalWidth, modalHeight);
        panel.Position = (viewport - panel.Size) / 2f;
        DrawDeployPaths();
    }

    private VBoxContainer CommandCenterSection(string heading)
    {
        var section = new VBoxContainer();
        section.AddThemeConstantOverride("separation", 7);
        var label = MakeLabel(heading, 17, GoldBright);
        section.AddChild(label);
        return section;
    }

    private Button CommandCenterButton(string text, bool active, Vector2 size)
    {
        var button = MakeButton(text);
        button.CustomMinimumSize = size;
        button.AddThemeFontSizeOverride("font_size", 15);
        var normal = GD.Load<Texture2D>(DeployCommandAssetRoot + (active ? "hover_active_btn.png" : "normal_btn.png"));
        var hover = GD.Load<Texture2D>(DeployCommandAssetRoot + "hover_active_btn.png");
        button.AddThemeStyleboxOverride("normal", CommandCenterTextureBox(normal));
        button.AddThemeStyleboxOverride("hover", CommandCenterTextureBox(hover));
        button.AddThemeStyleboxOverride("pressed", CommandCenterTextureBox(hover));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.AddThemeColorOverride("font_color", GoldBright);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        return button;
    }

    private static StyleBoxTexture CommandCenterTextureBox(Texture2D texture)
        => new() { Texture = texture, ContentMarginLeft = 12, ContentMarginRight = 12,
            ContentMarginTop = 5, ContentMarginBottom = 5 };

    private void ApplyDeployComposeButtonTexture(Button button, bool active)
    {
        var normal = GD.Load<Texture2D>(DeployCommandAssetRoot
            + (active ? "hover_active_btn.png" : "normal_btn.png"));
        var hover = GD.Load<Texture2D>(DeployCommandAssetRoot + "hover_active_btn.png");
        button.AddThemeStyleboxOverride("normal", CommandCenterTextureBox(normal));
        button.AddThemeStyleboxOverride("hover", CommandCenterTextureBox(hover));
        button.AddThemeStyleboxOverride("pressed", CommandCenterTextureBox(hover));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.AddThemeColorOverride("font_color", GoldBright);
    }

    private Control DeployComposeResourceHeading(string iconFile, string text)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 7);
        var icon = new TextureRect
        {
            Texture = GD.Load<Texture2D>(DeployCommandAssetRoot + iconFile),
            CustomMinimumSize = new Vector2(28, 28),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        row.AddChild(icon);
        var label = MakeLabel(text, 13, GoldBright);
        label.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(label);
        return row;
    }

    private void RefreshDeployComposeHeroCards()
    {
        if (_deployComposeHeroCards is null || !GodotObject.IsInstanceValid(_deployComposeHeroCards)) return;
        foreach (var child in _deployComposeHeroCards.GetChildren())
        {
            _deployComposeHeroCards.RemoveChild(child);
            child.QueueFree();
        }
        _deployComposeHeroCards.AddChild(BuildDeployComposeHeroCard("선봉", _depVan));
        _deployComposeHeroCards.AddChild(BuildDeployComposeHeroCard("부관", _depAdj));
    }

    private Control BuildDeployComposeHeroCard(string role, SanguoSLG.Core.Domain.GeneralId? generalId)
    {
        var card = new Control
        {
            CustomMinimumSize = new Vector2(285, 108),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClipContents = true,
        };
        var background = new TextureRect
        {
            Texture = GD.Load<Texture2D>(DeployCommandAssetRoot + "hero_card.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        background.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        card.AddChild(background);
        var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        card.AddChild(margin);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 10);
        margin.AddChild(row);
        if (generalId is not { } id)
        {
            var empty = MakeLabel($"{role}\n선택되지 않음", 14, Parchment);
            empty.VerticalAlignment = VerticalAlignment.Center;
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(empty);
            return card;
        }
        var general = _state.Generals.First(item => item.Id == id);



        // var portrait = FixedIcon(OfficerPortrait(id), 82);
        // portrait.CustomMinimumSize = new Vector2(82, 82);
        // portrait.MouseFilter = Control.MouseFilterEnum.Ignore;
        // row.AddChild(portrait);

        const int portraitSize = 74;
        const int borderSize = 1;

        // 장수 초상화
        var portrait = new TextureRect
        {
            Texture = OfficerPortrait(id),
            CustomMinimumSize = new Vector2(portraitSize, portraitSize),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        // 테두리 컨테이너
        var portraitFrame = new PanelContainer
        {
            CustomMinimumSize = new Vector2(
                portraitSize + borderSize * 2,
                portraitSize + borderSize * 2
            ),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        var frameStyle = new StyleBoxFlat
        {
            BgColor = new Color("#17120E"),
            BorderColor = new Color("#B89452"),
            BorderWidthLeft = borderSize,
            BorderWidthRight = borderSize,
            BorderWidthTop = borderSize,
            BorderWidthBottom = borderSize,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
            ContentMarginLeft = borderSize,
            ContentMarginRight = borderSize,
            ContentMarginTop = borderSize,
            ContentMarginBottom = borderSize,
        };

        portraitFrame.AddThemeStyleboxOverride("panel", frameStyle);
        portraitFrame.AddChild(portrait);

        row.AddChild(portraitFrame);        
  

        var text = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        text.AddChild(MakeLabel($"{role}   {general.Name}", 17, GoldBright));
        text.AddChild(MakeLabel($"적성 {GradeText(SelectedDeployAptitude(general))}", 13, Parchment));
        text.AddChild(MakeLabel($"무 {general.Might} · 지 {general.Intellect} · 정 {general.Politics}", 13, Parchment));
        row.AddChild(text);
        return card;
    }

private Control BuildDeployCommandCard(int index)
    {
        var request = _pendingDeploys[index].Req;
        var troop = _troops.FirstOrDefault(item => item.Code == request.TroopCode);
        var vanguard = _state.Generals.First(general => general.Id == request.Vanguard).Name;
        var adjutant = request.Adjutant is { } adj
            ? _state.Generals.First(general => general.Id == adj).Name : "없음";
        var selected = index == _depSelectedUnit;
        var button = new Button
        {
            Name = $"DeployCommandUnitCard{index}",
            Text = "",
            FocusMode = Control.FocusModeEnum.None,
            SelfModulate = Colors.White,
            CustomMinimumSize = new Vector2(377, 112),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
        };
        var normal = GD.Load<Texture2D>(DeployCommandAssetRoot + (selected ? "troop_hover_active.png" : "troop_normal.png"));
        var active = GD.Load<Texture2D>(DeployCommandAssetRoot + "troop_hover_active.png");
        button.AddThemeStyleboxOverride("normal", CommandCenterTextureBox(normal));
        button.AddThemeStyleboxOverride("hover", CommandCenterTextureBox(active));
        button.AddThemeStyleboxOverride("pressed", CommandCenterTextureBox(active));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.Pressed += () => { _depSelectedUnit = index; OpenDeployHub(); };

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.Minsize, 18);
        row.AddThemeConstantOverride("separation", 10);

        // [핵심 해결] art를 품고 있는 전용 컨테이너를 만들어서, 이 컨테이너 자체에 왼쪽 여백(Margin)을 줍니다.
        var artHolder = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        artHolder.AddThemeConstantOverride("margin_left", 30); // 이 숫자를 조절하면 이미지만 오른쪽으로 확실하게 이동합니다!
        artHolder.AddThemeConstantOverride("margin_top", 0);
        artHolder.AddThemeConstantOverride("margin_right", 0);
        artHolder.AddThemeConstantOverride("margin_bottom", 0);

        var art = FixedIcon(UnitCard(request.TroopCode, troop?.Class), 68);
        art.CustomMinimumSize = new Vector2(68, 68);
        art.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        art.MouseFilter = Control.MouseFilterEnum.Ignore;
        
        artHolder.AddChild(art);
        row.AddChild(artHolder); // 기존 art 대신 여백이 적용된 artHolder를 추가

        var info = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        info.AddChild(MakeLabel($"{troop?.Name ?? request.TroopCode}  {request.Troops:N0}명", 17, GoldBright));
        info.AddChild(MakeLabel($"{vanguard} / {adjutant}", 13, Parchment));
        var target = request.Target is { } hex
            ? _state.Cities.FirstOrDefault(candidate => candidate.Position == hex)?.Name ?? $"({hex.Q},{hex.R})"
            : "목표 미지정";
        info.AddChild(MakeLabel($"{ModeName(request.Mode)} · {DelayLabel(request.DelayDays)} · {target}", 12,
            request.Target is null ? new Color("e08068") : GoldBright));
        row.AddChild(info);
        button.AddChild(row);
        return button;
    }
private void BuildDeployCommandDetails(VBoxContainer parent, SanguoSLG.Core.Domain.CityId cityId)
    {
        if (_depSelectedUnit < 0 || _depSelectedUnit >= _pendingDeploys.Count)
        {
            var empty = MakeLabel("왼쪽에서 예약 부대를 선택하세요.", 16, Parchment);
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.VerticalAlignment = VerticalAlignment.Center;
            empty.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            parent.AddChild(empty);
            return;
        }
        var index = _depSelectedUnit;
        var request = _pendingDeploys[index].Req;
        var troop = _troops.FirstOrDefault(item => item.Code == request.TroopCode);
        var vanguard = _state.Generals.First(general => general.Id == request.Vanguard);
        var adjutant = request.Adjutant is { } adj
            ? _state.Generals.FirstOrDefault(general => general.Id == adj) : null;

        var overview = new PanelContainer();
        overview.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 5, 10));
        var overviewRow = new HBoxContainer();
        overviewRow.AddThemeConstantOverride("separation", 12);
        overviewRow.AddChild(DeployUnitArtwork(UnitCard(request.TroopCode, troop?.Class), 76));
        var overviewText = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        overviewText.AddChild(MakeLabel($"{troop?.Name ?? request.TroopCode}  {request.Troops:N0}명", 21, GoldBright));
        overviewText.AddChild(MakeLabel($"선봉 {vanguard.Name} / 부장 {adjutant?.Name ?? "없음"}", 14, Parchment));
        overviewRow.AddChild(overviewText);
        overview.AddChild(overviewRow);
        parent.AddChild(overview);

        var officers = new PanelContainer();
        officers.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 5, 10));
        var officerText = $"편성 장수\n선봉  {vanguard.Name}   무 {vanguard.Might} · 지 {vanguard.Intellect} · 정 {vanguard.Politics}";
        if (adjutant is not null) officerText += $"\n부장  {adjutant.Name}   무 {adjutant.Might} · 지 {adjutant.Intellect} · 정 {adjutant.Politics}";
        officers.AddChild(MakeLabel(officerText, 14, Parchment));
        parent.AddChild(officers);

        // 부대 병력(request.Troops)과 1만 명당 소모량 기준을 직접 반영
        // 1. 설정에서 1만 명당 하루 군량 소모량 가져오기 (프로젝트 내 보유 중인 state나 매니저 변수명 활용)
        var _balanced = new BalanceConfig(MonthlyTaxPerCity: 100);
        int provPer10k = _balanced.ProvisionsPer10kPerDay; // _state 구조에 Balance가 있는 경우; // _state의 실제 변수명에 맞춰 조정
        // 2. 현재 부대 병력 기준 1일 소모량 및 10일치 등 계산에 활용
        var troopCount = request.Troops;

        // (참고) 만약 '휴대 군량으로 버틸 수 있는 총 일수'를 구하는 거라면:
        int supplyDays = (troopCount > 0 && provPer10k > 0) 
            ? Mathf.RoundToInt((request.Provisions * 10000f) / (troopCount * provPer10k)) 
            : 0;

        var supply = new PanelContainer();
        supply.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 5, 10));
        supply.AddChild(MakeLabel($"보급 현황\n휴대 군량 {request.Provisions:N0} · 약 {supplyDays}일\n휴대 금 {request.Gold:N0}", 14, Parchment));
        parent.AddChild(supply);     

        // var supply = new PanelContainer();
        // supply.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 5, 10));
        // supply.AddChild(MakeLabel($"보급 현황\n휴대 군량 {request.Provisions:N0} · 약 {SupplyProvisionDaysFromAmount(request.Provisions)}일\n휴대 금 {request.Gold:N0}", 14, Parchment));
        // parent.AddChild(supply);

        // 1. "출전 명령" 영역의 높이 확장(ExpandFill)을 제거하여 영역을 줄임
        var command = new PanelContainer();
        command.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 5, 10));
        var commandBox = new VBoxContainer();
        commandBox.AddThemeConstantOverride("separation", 6); // 내부 간격도 살짝 줄임
        commandBox.AddChild(MakeLabel("출전 명령", 16, GoldBright));
        
        var modeRow = new HBoxContainer();
        modeRow.AddThemeConstantOverride("separation", 7);
        foreach (var (label, mode) in new[] { ("행군", UnitMode.March), ("전진", UnitMode.Advance), ("공격", UnitMode.Attack) })
        {
            var selectedMode = request.Mode == mode;
            var modeButton = CommandCenterButton(label, selectedMode, new Vector2(92, 36));
            modeButton.Pressed += () =>
            {
                _pendingDeploys[index] = (_pendingDeploys[index].Req with { Mode = mode }, _pendingDeploys[index].Label);
                foreach (var child in modeRow.GetChildren().OfType<Button>())
                    ApplyDeployComposeButtonTexture(child, child == modeButton);
            };
            modeRow.AddChild(modeButton);
        }
        commandBox.AddChild(modeRow);
        
        var delayRow = new HBoxContainer();
        delayRow.AddChild(MakeLabel("출전 시점", 14, Parchment));
        var delay = new OptionButton { Name = "DeployCommandDelay", CustomMinimumSize = new Vector2(160, 36),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        delay.AddThemeFontOverride("font", _font);
        for (var days = 0; days <= DeployService.MaxDeploymentDelayDays; days++)
            delay.AddItem(days == 0 ? "당일 출격" : $"{days}일 뒤 출격", days);
        delay.Select(request.DelayDays);
        delay.ItemSelected += selected =>
        {
            _pendingDeploys[index] = (_pendingDeploys[index].Req with { DelayDays = (int)selected }, _pendingDeploys[index].Label);
        };
        delayRow.AddChild(delay);
        commandBox.AddChild(delayRow);
        
        var targetRow = new HBoxContainer();
        var targetText = request.Target is { } target
            ? _state.Cities.FirstOrDefault(candidate => candidate.Position == target)?.Name ?? $"({target.Q},{target.R})"
            : "미지정";
        var targetLabel = MakeLabel($"현재 목표: {targetText}", 14, request.Target is null ? new Color("e08068") : GoldBright);
        targetLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        targetRow.AddChild(targetLabel);
        var targetButton = CommandCenterButton("목표 지정", true, new Vector2(132, 38));
        targetButton.Pressed += () => BeginTargeting(index);
        targetRow.AddChild(targetButton);
        commandBox.AddChild(targetRow);
        
        command.AddChild(commandBox);
        parent.AddChild(command);

        // 2. "출전 명령"과 "편성 요약" 사이에 간격을 주기 위해 MarginContainer 추가
        var summarySpacer = new Control { CustomMinimumSize = new Vector2(0, 8) }; // 간격 크기 (8px)
        parent.AddChild(summarySpacer);

        // 3. "편성 요약"을 중간 영역(parent) 내부의 맨 아래로 이동
        var summary = new PanelContainer { CustomMinimumSize = new Vector2(0, 42) };
        summary.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 4, 10));
        summary.AddChild(MakeLabel(DeployCommandSummary(), 14, GoldBright));
        parent.AddChild(summary);
    }
    private string DeployCommandSummary()
    {
        if (_depSelectedUnit < 0 || _depSelectedUnit >= _pendingDeploys.Count) return "편성 요약  ·  선택된 예약 부대 없음";
        var request = _pendingDeploys[_depSelectedUnit].Req;
        var troop = _troops.FirstOrDefault(item => item.Code == request.TroopCode)?.Name ?? request.TroopCode;
        var vanguard = _state.Generals.First(general => general.Id == request.Vanguard).Name;
        return $"편성 요약  ·  {troop} {request.Troops:N0}명  |  {vanguard}  |  {ModeName(request.Mode)}  |  군량 {request.Provisions:N0}  |  금 {request.Gold:N0}";
    }

    private async void RunDeployCommandCenterQa()
    {
        var sizeArgument = OS.GetCmdlineUserArgs().FirstOrDefault(argument =>
            argument.StartsWith("--maptestdeploycommandcenterqa=", System.StringComparison.Ordinal));
        if (sizeArgument is not null)
        {
            var dimensions = sizeArgument.Split('=', 2)[1].Split('x', 2);
            if (dimensions.Length == 2 && int.TryParse(dimensions[0], out var width)
                && int.TryParse(dimensions[1], out var height))
                GetWindow().Size = new Vector2I(width, height);
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var city = _state.Cities.First(candidate => candidate.Owner == Player
            && _state.Garrisons.Any(garrison => garrison.City == candidate.Id && garrison.Troops > 0)
            && _state.GeneralsAt(candidate.Id).Any());
        var garrison = _state.Garrisons.First(item => item.City == city.Id && item.Troops > 0);
        var general = _state.GeneralsAt(city.Id).First();
        var originalCount = _pendingDeploys.Count;
        _depModalCity = city.Id;
        _pendingDeploys.Add((new DeployRequest(city.Id, garrison.TroopCode,
            System.Math.Min(1000, garrison.Troops), general, Provisions: 100, Gold: 50), "지휘소 QA"));
        _depSelectedUnit = _pendingDeploys.Count - 1;
        OpenDeployHub();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var center = _modalLayer?.FindChild("DeployCommandCenter", true, false) as Control;
        var background = _modalLayer?.FindChild("DeployCommandCenterBackground", true, false) as TextureRect;
        var columns = _modalLayer?.FindChild("DeployCommandCenterColumns", true, false) as HBoxContainer;
        var list = _modalLayer?.FindChild("DeployCommandUnitList", true, false) as ScrollContainer;
        var card = _modalLayer?.FindChild($"DeployCommandUnitCard{_depSelectedUnit}", true, false) as Button;
        var delay = _modalLayer?.FindChild("DeployCommandDelay", true, false) as OptionButton;
        var labels = _modalLayer?.FindChildren("*", "Label", true, false).OfType<Label>()
            .Select(label => label.Text).ToArray() ?? [];
        var buttons = _modalLayer?.FindChildren("*", "Button", true, false).OfType<Button>().ToList() ?? [];
        var textureOk = background?.Texture?.ResourcePath.EndsWith("deploy_command_panel.png",
            System.StringComparison.Ordinal) == true;
        var layoutOk = center is not null && columns is not null && list is not null && card is not null
            && center.Size.X <= GetViewport().GetVisibleRect().Size.X
            && center.Size.Y <= GetViewport().GetVisibleRect().Size.Y
            && center.Size.X > center.Size.Y;
        var centerSize = center?.Size ?? Vector2.Zero;
        var contentOk = labels.Any(text => text.Contains("출전 지휘소", System.StringComparison.Ordinal))
            && labels.Any(text => text.Contains("선택 부대 정보", System.StringComparison.Ordinal))
            && buttons.Any(button => button.Text == "신규 편성" || button.Text.Contains("신규 편성", System.StringComparison.Ordinal))
            && buttons.Any(button => button.Text == "목표 지정")
            && buttons.Any(button => button.Text == "편성 수정")
            && buttons.Any(button => button.Text == "부대 삭제");
        var delayOk = delay is not null && delay.ItemCount == DeployService.MaxDeploymentDelayDays + 1;
        if (delayOk)
        {
            delay!.EmitSignal(OptionButton.SignalName.ItemSelected, 2);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            delayOk = _pendingDeploys[_depSelectedUnit].Req.DelayDays == 2;
        }
        var passed = textureOk && layoutOk && contentOk && delayOk;
        GD.Print($"[deploy-command-center-qa] passed={passed} texture={textureOk} layout={layoutOk} content={contentOk} delay={delayOk} size={centerSize}");
        while (_pendingDeploys.Count > originalCount) _pendingDeploys.RemoveAt(_pendingDeploys.Count - 1);
        CloseModal();
        GetTree().Quit(passed ? 0 : 1);
    }

    private async void RunDeployComposeRenewalQa()
    {
        var sizeArgument = OS.GetCmdlineUserArgs().FirstOrDefault(argument =>
            argument.StartsWith("--maptestdeploycomposerenewalqa=", System.StringComparison.Ordinal));
        if (sizeArgument is not null)
        {
            var dimensions = sizeArgument.Split('=', 2)[1].Split('x', 2);
            if (dimensions.Length == 2 && int.TryParse(dimensions[0], out var width)
                && int.TryParse(dimensions[1], out var height)) GetWindow().Size = new Vector2I(width, height);
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var city = _state.Cities.First(candidate => candidate.Owner == Player
            && _state.Garrisons.Any(garrison => garrison.City == candidate.Id && garrison.Troops > 0 && !garrison.Trainee)
            && _state.GeneralsAt(candidate.Id).Any(id => !_state.IsGeneralBusy(id)));
        _depModalCity = city.Id;
        OpenDeployCompose(-1);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var background = _modalLayer?.FindChild("DeployComposeBackground", true, false) as TextureRect;
        var frame = _modalLayer?.FindChild("DeployComposeModal", true, false) as DeployComposeModal;
        var safeArea = _modalLayer?.FindChild("DeployComposeSafeArea", true, false) as MarginContainer;
        var columns = _modalLayer?.FindChild("DeployComposeColumns", true, false) as HBoxContainer;
        var troopGrid = _modalLayer?.FindChild("DeployComposeTroopGrid", true, false) as GridContainer;
        var heroCards = _modalLayer?.FindChild("DeployComposeHeroCards", true, false) as HBoxContainer;
        var table = _modalLayer?.FindChild("ComposeOfficerTable", true, false) as Tree;
        var leftScroll = _modalLayer?.FindChild("DeployComposeResourcesScroll", true, false) as ScrollContainer;
        var resourceTextures = _modalLayer?.FindChildren("*", "TextureRect", true, false).OfType<TextureRect>()
            .Select(texture => texture.Texture?.ResourcePath ?? "").ToArray() ?? [];
        var structureOk = background?.Texture?.ResourcePath.EndsWith("deploy_command_panel.png",
                System.StringComparison.Ordinal) == true
            && frame is not null && safeArea is not null
            && frame.GetGlobalRect().Encloses(safeArea.GetGlobalRect())
            && columns?.GetChildCount() == 2 && troopGrid?.Columns == 2
            && heroCards?.GetChildCount() == 2 && table?.Columns == 8
            && leftScroll?.VerticalScrollMode == ScrollContainer.ScrollMode.Auto;
        var assetsOk = resourceTextures.Any(path => path.EndsWith("food.png", System.StringComparison.Ordinal))
            && resourceTextures.Any(path => path.EndsWith("money.png", System.StringComparison.Ordinal))
            && resourceTextures.Count(path => path.EndsWith("hero_card.png", System.StringComparison.Ordinal)) == 2;
        var noSearch = _modalLayer?.FindChildren("*Search*", "LineEdit", true, false).Count == 0;
        var firstGeneral = _composeFree.FirstOrDefault();
        if (firstGeneral != default)
        {
            _depVan = firstGeneral;
            RefreshDeployComposeHeroCards();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        var labels = _modalLayer?.FindChildren("*", "Label", true, false).OfType<Label>()
            .Select(label => label.Text).ToArray() ?? [];
        var liveSelection = firstGeneral == default || labels.Any(text =>
            text.Contains(_state.Generals.First(general => general.Id == firstGeneral).Name,
                System.StringComparison.Ordinal));
        var passed = structureOk && assetsOk && noSearch && liveSelection;
        GD.Print($"[deploy-compose-renewal-qa] passed={passed} structure={structureOk} assets={assetsOk} noSearch={noSearch} live={liveSelection}");
        CloseModal();
        GetTree().Quit(passed ? 0 : 1);
    }

    private async void RunSupplyComposeRenewalQa()
    {
        var sizeArgument = OS.GetCmdlineUserArgs().FirstOrDefault(argument =>
            argument.StartsWith("--maptestsupplycomposerenewalqa=", System.StringComparison.Ordinal));
        if (sizeArgument is not null)
        {
            var dimensions = sizeArgument.Split('=', 2)[1].Split('x', 2);
            if (dimensions.Length == 2 && int.TryParse(dimensions[0], out var width)
                && int.TryParse(dimensions[1], out var height)) GetWindow().Size = new Vector2I(width, height);
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var city = _state.Cities.First(candidate => candidate.Owner == Player
            && _state.Garrisons.Any(garrison => garrison.City == candidate.Id && garrison.Troops > 0 && !garrison.Trainee)
            && _state.GeneralsAt(candidate.Id).Any(id => !_state.IsGeneralBusy(id)));
        _depModalCity = city.Id;
        OpenSupplyCompose(-1);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var modal = _modalLayer?.FindChild("SupplyComposeModal", true, false) as SupplyComposeModal;
        var background = _modalLayer?.FindChild("SupplyComposeBackground", true, false) as TextureRect;
        var safeArea = _modalLayer?.FindChild("SupplyComposeSafeArea", true, false) as MarginContainer;
        var columns = _modalLayer?.FindChild("SupplyComposeColumns", true, false) as HBoxContainer;
        var leftScroll = _modalLayer?.FindChild("SupplyComposeResourcesScroll", true, false) as ScrollContainer;
        var table = _modalLayer?.FindChild("ComposeOfficerTable", true, false) as Tree;
        var footer = _modalLayer?.FindChild("SupplyComposeFooter", true, false) as VBoxContainer;
        var delay = _modalLayer?.FindChild("DeploymentDelaySelector", true, false) as OptionButton;
        var buttons = _modalLayer?.FindChildren("*", "Button", true, false).OfType<Button>().ToArray() ?? [];
        var labels = _modalLayer?.FindChildren("*", "Label", true, false).OfType<Label>()
            .Select(label => label.Text).ToArray() ?? [];

        var structureOk = modal is not null && safeArea is not null
            && modal.GetGlobalRect().Encloses(safeArea.GetGlobalRect())
            && background?.Texture?.ResourcePath.EndsWith("deploy_command_panel.png",
                System.StringComparison.Ordinal) == true
            && columns?.GetChildCount() == 2
            && leftScroll?.VerticalScrollMode == ScrollContainer.ScrollMode.Auto
            && table?.Columns == 7 && table.ScrollVerticalEnabled
            && footer is not null;
        var contentOk = labels.Any(text => text.Contains("보급 병력 선택", System.StringComparison.Ordinal))
            && labels.Any(text => text.Contains("주장 선택", System.StringComparison.Ordinal))
            && buttons.Any(button => button.Text.Contains("확인", System.StringComparison.Ordinal))
            && delay?.ItemCount == DeployService.MaxDeploymentDelayDays + 1;
        var viewportRect = GetViewport().GetVisibleRect();
        var boundsOk = modal is not null && viewportRect.Encloses(modal.GetGlobalRect());
        var passed = structureOk && contentOk && boundsOk;
        GD.Print($"[supply-compose-renewal-qa] passed={passed} structure={structureOk} content={contentOk} bounds={boundsOk} size={modal?.Size}");
        CloseModal();
        GetTree().Quit(passed ? 0 : 1);
    }
}
