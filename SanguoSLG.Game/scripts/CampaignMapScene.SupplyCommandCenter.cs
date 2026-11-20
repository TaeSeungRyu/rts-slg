using System.Collections.Generic;
using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    // 보급 지휘소 전용 레이아웃. 크기와 간격의 단위는 px입니다.
    private static class SupplyHubLayout
    {
        // 모달 크기와 전체 안쪽 여백
        public const float ViewportInset = 14f; // 화면 가장자리 기준, 한쪽당
        public const float MaxPanelHeight = 970f;
        public const float PanelAspectRatio = 1.5f;
        public const int PaddingLeft = 45;
        public const int PaddingRight = 45;
        public const int PaddingTop = 13;
        public const int PaddingBottom = 24;
        public const int MainRowGap = 8; // 헤더, 상태줄, 본문, 푸터 사이 공통 간격

        // 헤더와 상태줄
        public const int HeaderHeight = 76;
        public const int HeaderGap = 10;
        public const int HeaderLeadingSpace = 28; // HeaderGap이 추가됩니다.
        public static readonly Vector2 CloseButtonSize = new(30, 30);
        public const int StatusHeight = 46;
        public const int StatusGap = 18;
        public static readonly Vector2 NewUnitButtonSize = new(170, 42);

        // 본문 열과 예약 부대 카드
        public const int ColumnGap = 12;
        public const int ListWidth = 405;
        public const int ListCardGap = 8;
        public static readonly int? ListHeadingGap = null; // null이면 공용 섹션 설정 사용
        public static readonly Vector2 CardSize = new(377, 112);
        public const int CardPadding = 18;
        public const int CardContentGap = 12;
        public const int CardIconSize = 68;

        // 선택 부대 상세
        public static readonly int? DetailSectionGap = null; // null이면 공용 섹션 설정 사용
        public const int DetailPanelPadding = 10;
        public const int DetailOverviewGap = 12;
        public const int DetailArtworkSize = 76;
        public const int CommandRowGap = 6;
        public const int ModeButtonGap = 7;
        public static readonly Vector2 ModeButtonSize = new(92, 36);
        public static readonly Vector2 DelayMinimumSize = new(160, 36);
        public static readonly Vector2 TargetButtonSize = new(132, 38);
        public const int SummaryHeight = 42;

        // 하단 버튼 영역
        public const int FooterHeight = 43;
        public const int FooterGap = 8;
        public static readonly Vector2 RemoveButtonSize = new(132, 42);
        public static readonly Vector2 EditButtonSize = new(145, 42);
        public static readonly Vector2 DoneButtonSize = new(190, 42);

        public const int NewUnitButtonTopMargin = 20;

        public const int CardExtraLeftPadding = 30;  //왼쪽 카드 영역 내부 패딩

    }

    private void OpenSupplyHub(CityId cityId)
    {
        _depModalCity = cityId;
        if (_modalLayer is not null) { FadeOutModal(_modalLayer); _modalLayer = null; }
        var layer = new CanvasLayer { Layer = 20 };
        AddChild(layer);
        _modalLayer = layer;
        var backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.68f), MouseFilter = Control.MouseFilterEnum.Stop };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(backdrop);

        var viewport = GetViewport().GetVisibleRect().Size;
        var height = Mathf.Min(viewport.Y - 2 * SupplyHubLayout.ViewportInset, SupplyHubLayout.MaxPanelHeight);
        var width = Mathf.Min(viewport.X - 2 * SupplyHubLayout.ViewportInset, height * SupplyHubLayout.PanelAspectRatio);
        var panel = new PanelContainer
        {
            Name = "SupplyCommandCenter",
            CustomMinimumSize = new Vector2(width, height),
            Size = new Vector2(width, height),
            Position = (viewport - new Vector2(width, height)) / 2f,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        panel.SetMeta("modal_root", true);
        layer.AddChild(panel);
        var background = new TextureRect
        {
            Name = "SupplyCommandCenterBackground",
            Texture = GD.Load<Texture2D>(DeployCommandAssetRoot + "deploy_command_panel.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        panel.AddChild(background);
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", SupplyHubLayout.PaddingLeft);
        margin.AddThemeConstantOverride("margin_right", SupplyHubLayout.PaddingRight);
        margin.AddThemeConstantOverride("margin_top", SupplyHubLayout.PaddingTop);
        margin.AddThemeConstantOverride("margin_bottom", SupplyHubLayout.PaddingBottom);
        panel.AddChild(margin);
        var root = new VBoxContainer { Name = "SupplyCommandCenterContent" };
        root.AddThemeConstantOverride("separation", SupplyHubLayout.MainRowGap);
        margin.AddChild(root);

        var cityName = _state.Cities.First(item => item.Id == cityId).Name;
        var mine = Enumerable.Range(0, _pendingSupplyDeploys.Count)
            .Where(index => _pendingSupplyDeploys[index].Req.City == cityId).ToList();
        if (!mine.Contains(_depSelectedUnit)) _depSelectedUnit = mine.FirstOrDefault(-1);
        var queued = _state.Armies.Count(unit => unit.OriginCity == cityId
            && unit.IsWaitingDeployment && unit.Field.Owner == Player);

        var header = BuildSupplyHubHeader(cityId, cityName);
        root.AddChild(header);
        root.AddChild(BuildSupplyHubStatus(mine.Count, queued));
        root.AddChild(BuildSupplyHubColumns(cityId, mine));
        root.AddChild(BuildSupplyHubFooter(cityId));

        CenterAndDrag(panel, header, width, height, root);
        panel.Size = new Vector2(width, height);
        panel.Position = (viewport - panel.Size) / 2f;
        DrawDeployPaths();
    }

    private HBoxContainer BuildSupplyHubHeader(CityId cityId, string cityName)
    {
        var header = new HBoxContainer { CustomMinimumSize = new Vector2(0, SupplyHubLayout.HeaderHeight) };
        header.AddThemeConstantOverride("separation", SupplyHubLayout.HeaderGap);
        header.AddChild(new Control { CustomMinimumSize = new Vector2(SupplyHubLayout.HeaderLeadingSpace, 0), MouseFilter = Control.MouseFilterEnum.Ignore });
        var title = MakeLabel($"보급 지휘소  《 {cityName} 》", 27, GoldBright);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.VerticalAlignment = VerticalAlignment.Center;
        title.MouseFilter = Control.MouseFilterEnum.Ignore;
        header.AddChild(title);
        var close = MakeButton("×");
        close.CustomMinimumSize = new Vector2(30, 30);
        close.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        close.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        close.AddThemeFontSizeOverride("font_size", 18);
        close.AddThemeStyleboxOverride("normal", Frame(Colors.Transparent, Gold, 1, 4, 2));
        close.AddThemeStyleboxOverride("hover", Frame(new Color("5b1712"), GoldBright, 1, 4, 2));
        close.AddThemeStyleboxOverride("pressed", Frame(new Color("35100d"), GoldBright, 1, 4, 2));
        close.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());        
        close.Name = "SupplyCommandClose";
        close.Pressed += () => { CloseModal(); SelectCity(cityId); };
        header.AddChild(close);
        return header;
    }

    private HBoxContainer BuildSupplyHubStatus(int reservedCount, int queued)
    {
        var status = new HBoxContainer { CustomMinimumSize = new Vector2(0, SupplyHubLayout.StatusHeight) };
        status.AddThemeConstantOverride("separation", SupplyHubLayout.StatusGap);
        status.AddChild(MakeLabel($"예약된 보급부대  {reservedCount}", 17, GoldBright));
        status.AddChild(MakeLabel("│", 17, Gold));
        status.AddChild(MakeLabel($"출격 대기  {queued}", 17, GoldBright));
        status.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var add = CommandCenterButton("＋ 신규 편성", true, SupplyHubLayout.NewUnitButtonSize);
        add.Name = "SupplyCommandNewUnit";
        add.Pressed += () => OpenSupplyCompose(-1);
        var addMargin = new MarginContainer();
        addMargin.AddThemeConstantOverride("margin_top", SupplyHubLayout.NewUnitButtonTopMargin);
        addMargin.AddChild(add);
        status.AddChild(addMargin);        
        //status.AddChild(add);
        return status;
    }

    private HBoxContainer BuildSupplyHubColumns(CityId cityId, IReadOnlyList<int> reservedIndices)
    {
        var columns = new HBoxContainer
        {
            Name = "SupplyCommandCenterColumns",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        columns.AddThemeConstantOverride("separation", SupplyHubLayout.ColumnGap);
        var left = CommandCenterSection("예약 보급부대 목록");
        if (SupplyHubLayout.ListHeadingGap is int listHeadingGap)
            left.AddThemeConstantOverride("separation", listHeadingGap);
        left.CustomMinimumSize = new Vector2(SupplyHubLayout.ListWidth, 0);
        left.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        columns.AddChild(left);
        var listScroll = new ScrollContainer
        {
            Name = "SupplyCommandUnitList",
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        GameUiSkin.ApplyQuickListScroll(listScroll);
        left.AddChild(listScroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", SupplyHubLayout.ListCardGap);
        listScroll.AddChild(list);
        foreach (var index in reservedIndices)
            list.AddChild(BuildSupplyCommandCard(index, cityId));
        if (reservedIndices.Count == 0)
        {
            var empty = MakeLabel("예약된 보급부대가 없습니다.\n신규 편성으로 부대를 추가하세요.", 15, Parchment);
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            list.AddChild(empty);
        }

        var right = CommandCenterSection("선택 보급부대 정보");
        if (SupplyHubLayout.DetailSectionGap is int detailSectionGap)
            right.AddThemeConstantOverride("separation", detailSectionGap);
        right.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        right.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        columns.AddChild(right);
        BuildSupplyCommandDetails(right);
        return columns;
    }

    private HBoxContainer BuildSupplyHubFooter(CityId cityId)
    {
        var actions = new HBoxContainer { CustomMinimumSize = new Vector2(0, SupplyHubLayout.FooterHeight) };
        actions.AddThemeConstantOverride("separation", SupplyHubLayout.FooterGap);
        if (_depSelectedUnit >= 0 && _depSelectedUnit < _pendingSupplyDeploys.Count)
        {
            var selected = _depSelectedUnit;
            var remove = CommandCenterButton("부대 삭제", false, SupplyHubLayout.RemoveButtonSize);
            remove.Pressed += () =>
            {
                _pendingSupplyDeploys.RemoveAt(selected);
                _depSelectedUnit = -1;
                OpenSupplyHub(cityId);
            };
            actions.AddChild(remove);
            var edit = CommandCenterButton("편성 수정", false, SupplyHubLayout.EditButtonSize);
            edit.Pressed += () => OpenSupplyCompose(selected);
            actions.AddChild(edit);
        }
        actions.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var done = CommandCenterButton("확인", true, SupplyHubLayout.DoneButtonSize);
        done.Pressed += () => { CloseModal(); SelectCity(cityId); };
        actions.AddChild(done);
        return actions;
    }

    private Control BuildSupplyCommandCard(int index, CityId cityId)
    {
        var request = _pendingSupplyDeploys[index].Req;
        var total = request.Lines.Sum(line => line.Troops);
        var leader = _state.Generals.FirstOrDefault(general => general.Id == request.Vanguard)?.Name ?? "-";
        var selected = index == _depSelectedUnit;
        var button = new Button
        {
            Name = $"SupplyCommandUnitCard{index}",
            Text = "",
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = SupplyHubLayout.CardSize,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
        };
        button.AddThemeStyleboxOverride("normal", CommandCenterTextureBox(GD.Load<Texture2D>(DeployCommandAssetRoot
            + (selected ? "troop_hover_active.png" : "troop_normal.png"))));
        var active = CommandCenterTextureBox(GD.Load<Texture2D>(DeployCommandAssetRoot + "troop_hover_active.png"));
        button.AddThemeStyleboxOverride("hover", active);
        button.AddThemeStyleboxOverride("pressed", active);
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.Pressed += () => { _depSelectedUnit = index; OpenSupplyHub(cityId); };
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.Minsize, SupplyHubLayout.CardPadding);
        row.AddThemeConstantOverride("separation", SupplyHubLayout.CardContentGap);
        row.OffsetLeft += SupplyHubLayout.CardExtraLeftPadding;

        var art = FixedIcon(UnitCard("supply", TroopClass.Supply), SupplyHubLayout.CardIconSize);
        art.CustomMinimumSize = new Vector2(SupplyHubLayout.CardIconSize, SupplyHubLayout.CardIconSize);
        art.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        art.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.AddChild(art);
        var text = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        text.AddChild(MakeLabel($"보급부대  {total:N0}명", 17, GoldBright));
        text.AddChild(MakeLabel($"주장 {leader}", 13, Parchment));
        var target = request.Target is { } hex
            ? _state.Cities.FirstOrDefault(city => city.Position == hex)?.Name ?? $"({hex.Q},{hex.R})"
            : "목표 미지정";
        text.AddChild(MakeLabel($"{ModeName(request.Mode)} · {DelayLabel(request.DelayDays)} · {target}", 12, GoldBright));
        row.AddChild(text);
        button.AddChild(row);
        return button;
    }

    private void BuildSupplyCommandDetails(VBoxContainer parent)
    {
        if (_depSelectedUnit < 0 || _depSelectedUnit >= _pendingSupplyDeploys.Count)
        {
            var empty = MakeLabel("왼쪽에서 예약 보급부대를 선택하세요.", 16, Parchment);
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.VerticalAlignment = VerticalAlignment.Center;
            empty.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            parent.AddChild(empty);
            return;
        }
        var index = _depSelectedUnit;
        var request = _pendingSupplyDeploys[index].Req;
        var leader = _state.Generals.First(general => general.Id == request.Vanguard);
        var total = request.Lines.Sum(line => line.Troops);
        var roster = string.Join(" · ", request.Lines.Select(line =>
            $"{_troops.FirstOrDefault(troop => troop.Code == line.TroopCode)?.Name ?? line.TroopCode} {line.Troops:N0}"));
        var overview = new PanelContainer();
        overview.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 5, SupplyHubLayout.DetailPanelPadding));
        var overviewRow = new HBoxContainer();
        overviewRow.AddThemeConstantOverride("separation", SupplyHubLayout.DetailOverviewGap);
        overviewRow.AddChild(DeployUnitArtwork(UnitCard("supply", TroopClass.Supply), SupplyHubLayout.DetailArtworkSize));
        var overviewText = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        overviewText.AddChild(MakeLabel($"보급부대  {total:N0}명", 21, GoldBright));
        overviewText.AddChild(MakeLabel(roster, 13, Parchment));
        overviewRow.AddChild(overviewText);
        overview.AddChild(overviewRow);
        parent.AddChild(overview);

        var officer = new PanelContainer();
        officer.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 5, SupplyHubLayout.DetailPanelPadding));
        officer.AddChild(MakeLabel($"편성 장수\n주장  {leader.Name}   무 {leader.Might} · 지 {leader.Intellect} · 정 {leader.Politics}", 14, Parchment));
        parent.AddChild(officer);
        var supply = new PanelContainer();
        supply.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 5, SupplyHubLayout.DetailPanelPadding));
        supply.AddChild(MakeLabel($"보급 현황\n휴대 군량 {request.Provisions:N0} · 휴대 금 {request.Gold:N0}", 14, Parchment));
        parent.AddChild(supply);

        var command = new PanelContainer();
        command.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 5, SupplyHubLayout.DetailPanelPadding));
        var commandBox = new VBoxContainer();
        commandBox.AddThemeConstantOverride("separation", SupplyHubLayout.CommandRowGap);
        commandBox.AddChild(MakeLabel("출전 명령", 16, GoldBright));
        var modeRow = new HBoxContainer();
        modeRow.AddThemeConstantOverride("separation", SupplyHubLayout.ModeButtonGap);
        foreach (var (label, mode) in new[] { ("행군", UnitMode.March), ("전진", UnitMode.Advance), ("공격", UnitMode.Attack) })
        {
            var button = CommandCenterButton(label, request.Mode == mode, SupplyHubLayout.ModeButtonSize);
            button.Pressed += () =>
            {
                _pendingSupplyDeploys[index] = (_pendingSupplyDeploys[index].Req with { Mode = mode }, _pendingSupplyDeploys[index].Label);
                foreach (var sibling in modeRow.GetChildren().OfType<Button>())
                    ApplyDeployComposeButtonTexture(sibling, sibling == button);
            };
            modeRow.AddChild(button);
        }
        commandBox.AddChild(modeRow);
        var delayRow = new HBoxContainer();
        delayRow.AddChild(MakeLabel("출전 시점", 14, Parchment));
        var delay = new OptionButton { Name = "SupplyCommandDelay", CustomMinimumSize = SupplyHubLayout.DelayMinimumSize,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        delay.AddThemeFontOverride("font", _font);
        for (var days = 0; days <= DeployService.MaxDeploymentDelayDays; days++)
            delay.AddItem(days == 0 ? "당일 출격" : $"{days}일 뒤 출격", days);
        delay.Select(request.DelayDays);
        delay.ItemSelected += selected =>
            _pendingSupplyDeploys[index] = (_pendingSupplyDeploys[index].Req with { DelayDays = (int)selected }, _pendingSupplyDeploys[index].Label);
        delayRow.AddChild(delay);
        commandBox.AddChild(delayRow);
        var targetRow = new HBoxContainer();
        var target = request.Target is { } hex
            ? _state.Cities.FirstOrDefault(city => city.Position == hex)?.Name ?? $"({hex.Q},{hex.R})" : "미지정";
        var targetLabel = MakeLabel($"현재 목표: {target}", 14, GoldBright);
        targetLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        targetRow.AddChild(targetLabel);
        var targetButton = CommandCenterButton("목표 지정", true, SupplyHubLayout.TargetButtonSize);
        targetButton.Pressed += () => BeginSupplyTargeting(index);
        targetRow.AddChild(targetButton);
        commandBox.AddChild(targetRow);
        command.AddChild(commandBox);
        parent.AddChild(command);
        var summary = new PanelContainer { CustomMinimumSize = new Vector2(0, SupplyHubLayout.SummaryHeight) };
        summary.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 4, SupplyHubLayout.DetailPanelPadding));
        summary.AddChild(MakeLabel($"편성 요약 · 보급 {total:N0}명 | 주장 {leader.Name} | 군량 {request.Provisions:N0} | 금 {request.Gold:N0}", 14, GoldBright));
        parent.AddChild(summary);
    }

    private async void RunSupplyCommandCenterQa()
    {
        var argument = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--maptestsupplycommandcenterqa=", StringComparison.Ordinal));
        if (argument is not null)
        {
            var dimensions = argument.Split('=', 2)[1].Split('x', 2);
            if (dimensions.Length == 2 && int.TryParse(dimensions[0], out var width)
                && int.TryParse(dimensions[1], out var height)) GetWindow().Size = new Vector2I(width, height);
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var city = _state.Cities.First(candidate => candidate.Owner == Player);
        OpenSupplyHub(city.Id);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var panel = _modalLayer?.FindChild("SupplyCommandCenter", true, false) as PanelContainer;
        var background = _modalLayer?.FindChild("SupplyCommandCenterBackground", true, false) as TextureRect;
        var columns = _modalLayer?.FindChild("SupplyCommandCenterColumns", true, false) as HBoxContainer;
        var list = _modalLayer?.FindChild("SupplyCommandUnitList", true, false) as ScrollContainer;
        var add = _modalLayer?.FindChild("SupplyCommandNewUnit", true, false) as Button;
        var passed = panel is not null && GetViewport().GetVisibleRect().Encloses(panel.GetGlobalRect())
            && background?.Texture is not null && columns?.GetChildCount() == 2
            && list is not null && add is not null;
        GD.Print($"[supply-command-center-qa] passed={passed} size={panel?.Size} columns={columns?.GetChildCount()}");
        CloseModal();
        GetTree().Quit(passed ? 0 : 1);
    }
}
