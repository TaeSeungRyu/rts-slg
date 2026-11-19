using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
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
        var height = Mathf.Min(viewport.Y - 28f, 970f);
        var width = Mathf.Min(viewport.X - 28f, height * 1.5f);
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
        margin.AddThemeConstantOverride("margin_left", 45);
        margin.AddThemeConstantOverride("margin_right", 45);
        margin.AddThemeConstantOverride("margin_top", 13);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        panel.AddChild(margin);
        var root = new VBoxContainer { Name = "SupplyCommandCenterContent" };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var cityName = _state.Cities.First(item => item.Id == cityId).Name;
        var mine = Enumerable.Range(0, _pendingSupplyDeploys.Count)
            .Where(index => _pendingSupplyDeploys[index].Req.City == cityId).ToList();
        if (!mine.Contains(_depSelectedUnit)) _depSelectedUnit = mine.FirstOrDefault(-1);
        var queued = _state.Armies.Count(unit => unit.OriginCity == cityId
            && unit.IsWaitingDeployment && unit.Field.Owner == Player);

        var header = new HBoxContainer { CustomMinimumSize = new Vector2(0, 76) };
        header.AddThemeConstantOverride("separation", 10);
        header.AddChild(new Control { CustomMinimumSize = new Vector2(28, 0), MouseFilter = Control.MouseFilterEnum.Ignore });
        var title = MakeLabel($"보급 지휘소  《 {cityName} 》", 27, GoldBright);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.VerticalAlignment = VerticalAlignment.Center;
        title.MouseFilter = Control.MouseFilterEnum.Ignore;
        header.AddChild(title);
        var close = CommandCenterButton("×", false, new Vector2(30, 30));
        close.Name = "SupplyCommandClose";
        close.Pressed += () => { CloseModal(); SelectCity(cityId); };
        header.AddChild(close);
        root.AddChild(header);

        var status = new HBoxContainer { CustomMinimumSize = new Vector2(0, 46) };
        status.AddThemeConstantOverride("separation", 18);
        status.AddChild(MakeLabel($"예약된 보급부대  {mine.Count}", 17, GoldBright));
        status.AddChild(MakeLabel("│", 17, Gold));
        status.AddChild(MakeLabel($"출격 대기  {queued}", 17, GoldBright));
        status.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var add = CommandCenterButton("＋ 신규 편성", true, new Vector2(170, 42));
        add.Name = "SupplyCommandNewUnit";
        add.Pressed += () => OpenSupplyCompose(-1);
        status.AddChild(add);
        root.AddChild(status);

        var columns = new HBoxContainer
        {
            Name = "SupplyCommandCenterColumns",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        columns.AddThemeConstantOverride("separation", 12);
        root.AddChild(columns);
        var left = CommandCenterSection("예약 보급부대 목록");
        left.CustomMinimumSize = new Vector2(405, 0);
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
        list.AddThemeConstantOverride("separation", 8);
        listScroll.AddChild(list);
        foreach (var index in mine) list.AddChild(BuildSupplyCommandCard(index, cityId));
        if (mine.Count == 0)
        {
            var empty = MakeLabel("예약된 보급부대가 없습니다.\n신규 편성으로 부대를 추가하세요.", 15, Parchment);
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            list.AddChild(empty);
        }

        var right = CommandCenterSection("선택 보급부대 정보");
        right.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        right.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        columns.AddChild(right);
        BuildSupplyCommandDetails(right, cityId);

        var actions = new HBoxContainer { CustomMinimumSize = new Vector2(0, 46) };
        actions.AddThemeConstantOverride("separation", 8);
        if (_depSelectedUnit >= 0 && _depSelectedUnit < _pendingSupplyDeploys.Count)
        {
            var selected = _depSelectedUnit;
            var remove = CommandCenterButton("부대 삭제", false, new Vector2(132, 42));
            remove.Pressed += () =>
            {
                _pendingSupplyDeploys.RemoveAt(selected);
                _depSelectedUnit = -1;
                OpenSupplyHub(cityId);
            };
            actions.AddChild(remove);
            var edit = CommandCenterButton("편성 수정", false, new Vector2(145, 42));
            edit.Pressed += () => OpenSupplyCompose(selected);
            actions.AddChild(edit);
        }
        actions.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var done = CommandCenterButton("확인", true, new Vector2(190, 42));
        done.Pressed += () => { CloseModal(); SelectCity(cityId); };
        actions.AddChild(done);
        root.AddChild(actions);

        CenterAndDrag(panel, header, width, height, root);
        panel.Size = new Vector2(width, height);
        panel.Position = (viewport - panel.Size) / 2f;
        DrawDeployPaths();
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
            CustomMinimumSize = new Vector2(377, 112),
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
        row.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.Minsize, 18);
        row.AddThemeConstantOverride("separation", 12);
        var art = FixedIcon(UnitCard("supply", TroopClass.Supply), 68);
        art.CustomMinimumSize = new Vector2(68, 68);
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

    private void BuildSupplyCommandDetails(VBoxContainer parent, CityId cityId)
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
        overview.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 5, 10));
        var overviewRow = new HBoxContainer();
        overviewRow.AddThemeConstantOverride("separation", 12);
        overviewRow.AddChild(DeployUnitArtwork(UnitCard("supply", TroopClass.Supply), 76));
        var overviewText = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        overviewText.AddChild(MakeLabel($"보급부대  {total:N0}명", 21, GoldBright));
        overviewText.AddChild(MakeLabel(roster, 13, Parchment));
        overviewRow.AddChild(overviewText);
        overview.AddChild(overviewRow);
        parent.AddChild(overview);

        var officer = new PanelContainer();
        officer.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 5, 10));
        officer.AddChild(MakeLabel($"편성 장수\n주장  {leader.Name}   무 {leader.Might} · 지 {leader.Intellect} · 정 {leader.Politics}", 14, Parchment));
        parent.AddChild(officer);
        var supply = new PanelContainer();
        supply.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 5, 10));
        supply.AddChild(MakeLabel($"보급 현황\n휴대 군량 {request.Provisions:N0} · 휴대 금 {request.Gold:N0}", 14, Parchment));
        parent.AddChild(supply);

        var command = new PanelContainer();
        command.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 5, 10));
        var commandBox = new VBoxContainer();
        commandBox.AddThemeConstantOverride("separation", 6);
        commandBox.AddChild(MakeLabel("출전 명령", 16, GoldBright));
        var modeRow = new HBoxContainer();
        modeRow.AddThemeConstantOverride("separation", 7);
        foreach (var (label, mode) in new[] { ("행군", UnitMode.March), ("전진", UnitMode.Advance), ("공격", UnitMode.Attack) })
        {
            var button = CommandCenterButton(label, request.Mode == mode, new Vector2(92, 36));
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
        var delay = new OptionButton { Name = "SupplyCommandDelay", CustomMinimumSize = new Vector2(160, 36),
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
        var targetButton = CommandCenterButton("목표 지정", true, new Vector2(132, 38));
        targetButton.Pressed += () => BeginSupplyTargeting(index);
        targetRow.AddChild(targetButton);
        commandBox.AddChild(targetRow);
        command.AddChild(commandBox);
        parent.AddChild(command);
        var summary = new PanelContainer { CustomMinimumSize = new Vector2(0, 42) };
        summary.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 4, 10));
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
