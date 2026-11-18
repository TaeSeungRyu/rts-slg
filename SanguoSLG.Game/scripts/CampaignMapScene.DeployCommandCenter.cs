using Godot;
using SanguoSLG.Core.Simulation;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private const string DeployCommandAssetRoot = "res://assets/ui/game/deploy_command/";

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
        margin.AddThemeConstantOverride("margin_left", 35);
        margin.AddThemeConstantOverride("margin_right", 35);
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
            CustomMinimumSize = new Vector2(0, 96),
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
        header.AddChild(new Control { CustomMinimumSize = new Vector2(48, 0), MouseFilter = Control.MouseFilterEnum.Ignore });
        var title = MakeLabel($"출전 지휘소  《 {city.Name} 》", 27, GoldBright);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        title.VerticalAlignment = VerticalAlignment.Center;
        title.MouseFilter = Control.MouseFilterEnum.Ignore;
        header.AddChild(title);
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
        addOffset.AddThemeConstantOverride("margin_top", 5);
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
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 8);
        listScroll.AddChild(list);
        foreach (var index in mine) list.AddChild(BuildDeployCommandCard(index));
        if (mine.Count == 0)
        {
            var empty = MakeLabel("예약된 부대가 없습니다.\n신규 편성으로 부대를 추가하세요.", 15, Parchment);
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.VerticalAlignment = VerticalAlignment.Center;
            empty.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            list.AddChild(empty);
        }

        var right = CommandCenterSection("선택 부대 정보");
        right.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        right.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        content.AddChild(right);
        BuildDeployCommandDetails(right, cityId);

        var summary = new PanelContainer { CustomMinimumSize = new Vector2(0, 50) };
        summary.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 4, 10));
        summary.AddChild(MakeLabel(DeployCommandSummary(), 14, GoldBright));
        bottomRegion.AddChild(summary);

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
        var done = CommandCenterButton("출전 예약 확인", true, new Vector2(190, 42));
        done.Pressed += () => { CloseModal(); SelectCity(cityId); };
        actions.AddChild(done);
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
        var label = MakeLabel("◆ " + heading, 17, GoldBright);
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
        var art = FixedIcon(UnitCard(request.TroopCode, troop?.Class), 76);
        art.CustomMinimumSize = new Vector2(76, 76);
        art.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        art.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.AddChild(art);
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

        var supply = new PanelContainer();
        supply.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 5, 10));
        supply.AddChild(MakeLabel($"보급 현황\n휴대 군량 {request.Provisions:N0} · 약 {SupplyProvisionDaysFromAmount(request.Provisions)}일\n휴대 금 {request.Gold:N0}", 14, Parchment));
        parent.AddChild(supply);

        var command = new PanelContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        command.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 5, 10));
        var commandBox = new VBoxContainer();
        commandBox.AddThemeConstantOverride("separation", 8);
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
                OpenDeployHub();
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
            OpenDeployHub();
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
}
