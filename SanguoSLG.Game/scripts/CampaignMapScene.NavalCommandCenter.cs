using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private void OpenNavalHub(CityId city)
    {
        _depModalCity = city;
        var port = _state.Cities.First(item => item.Id == city);
        if (!port.IsPort)
        {
            ShowNotice("출항 불가", "출항은 항구에서만 사용할 수 있습니다.");
            SelectCity(city);
            return;
        }
        if (_modalLayer is not null) { FadeOutModal(_modalLayer); _modalLayer = null; }
        var layer = new CanvasLayer { Layer = 20 };
        AddChild(layer);
        _modalLayer = layer;
        var backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.68f), MouseFilter = Control.MouseFilterEnum.Stop };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(backdrop);

        var mine = Enumerable.Range(0, _pendingNavalDeploys.Count)
            .Where(index => _pendingNavalDeploys[index].Req.City == city).ToList();
        if (!mine.Contains(_depSelectedUnit)) _depSelectedUnit = mine.FirstOrDefault(-1);
        var frame = new DeploymentCommandFrame(GetViewport().GetVisibleRect().Size,
            GD.Load<Texture2D>(DeployCommandAssetRoot + "deploy_command_panel.png"), _font,
            $"출항 지휘소  《 {port.Name} 》", $"예약된 출항부대  {mine.Count}  |  저장 선박: {PortShipStockSummary(city)}",
            "예약 출항부대 목록", "선택 출항부대 정보");
        frame.Name = "NavalCommandCenter";
        layer.AddChild(frame);

        var close = frame.CloseButton;
        close.Text = "×";
        close.CustomMinimumSize = new Vector2(30, 30);
        close.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        close.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        close.AddThemeFontSizeOverride("font_size", 18);
        close.AddThemeStyleboxOverride("normal", Frame(Colors.Transparent, Gold, 1, 4, 2));
        close.AddThemeStyleboxOverride("hover", Frame(new Color("5b1712"), GoldBright, 1, 4, 2));
        close.AddThemeStyleboxOverride("pressed", Frame(new Color("35100d"), GoldBright, 1, 4, 2));
        close.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

        //ApplyDeployComposeButtonTexture(frame.CloseButton, false);
        //close.CloseButton.Pressed += () => { CloseModal(); SelectCity(city); };
        close.Pressed += () => { CloseModal(); SelectCity(city); };


        ApplyDeployComposeButtonTexture(frame.NewButton, true);
        frame.NewButton.Pressed += () => OpenNavalCompose(city);
        foreach (var index in mine) frame.List.AddChild(BuildNavalCommandCard(index, city));
        if (mine.Count == 0)
        {
            var empty = MakeLabel(
                "예약된 출항부대가 없습니다.\n신규 편성으로 부대를 추가하세요.",
                15,
                Parchment);

            empty.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            empty.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.VerticalAlignment = VerticalAlignment.Center;

            frame.List.AddChild(empty);
        }
        BuildNavalCommandDetails(frame.Details);
        if (_depSelectedUnit >= 0 && _depSelectedUnit < _pendingNavalDeploys.Count)
        {
            var selected = _depSelectedUnit;
            var remove = CommandCenterButton("부대 삭제", false, new Vector2(132, 42));
            remove.Pressed += () => { _pendingNavalDeploys.RemoveAt(selected); _depSelectedUnit = -1; OpenNavalHub(city); };
            frame.Actions.AddChild(remove);
        }
        frame.Actions.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var done = CommandCenterButton("확인", true, new Vector2(190, 42));
        done.Pressed += () => { CloseModal(); SelectCity(city); };
        frame.Actions.AddChild(done);
        CenterAndDrag(frame, frame.Header, frame.Size.X, frame.Size.Y, frame.Root);
        frame.Position = (GetViewport().GetVisibleRect().Size - frame.Size) / 2f;
    }

    private Control BuildNavalCommandCard(int index, CityId city)
    {
        var request = _pendingNavalDeploys[index].Req;
        var ship = _troops.FirstOrDefault(item => item.Code == request.ShipCode)?.Name ?? request.ShipCode;
        var leader = _state.Generals.FirstOrDefault(general => general.Id == request.Vanguard)?.Name ?? "-";
        var button = new Button
        {
            Name = $"NavalCommandCard{index}", Text = "", FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(377, 112), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
        };
        button.AddThemeStyleboxOverride("normal", CommandCenterTextureBox(GD.Load<Texture2D>(DeployCommandAssetRoot
            + (index == _depSelectedUnit ? "troop_hover_active.png" : "troop_normal.png"))));
        var active = CommandCenterTextureBox(GD.Load<Texture2D>(DeployCommandAssetRoot + "troop_hover_active.png"));
        button.AddThemeStyleboxOverride("hover", active);
        button.AddThemeStyleboxOverride("pressed", active);
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.Pressed += () => { _depSelectedUnit = index; OpenNavalHub(city); };
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        //row.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.Minsize, 18);
        row.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        row.OffsetLeft = 48;

        row.AddThemeConstantOverride("separation", 12);
        var art = FixedIcon(UnitCard(request.ShipCode, TroopClass.Naval), 68);
        art.CustomMinimumSize = new Vector2(68, 68);
        art.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        art.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.AddChild(art);
        var info = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        info.AddChild(MakeLabel($"{ship}  {request.Troops:N0}명", 17, GoldBright));
        info.AddChild(MakeLabel($"선봉 {leader}", 13, Parchment));
        info.AddChild(MakeLabel(NavalTargetName(request), 12, GoldBright));
        row.AddChild(info);
        button.AddChild(row);
        return button;
    }

    private string NavalTargetName(NavalDeployRequest request)
        => request.Target is { } target ? $"({target.Q},{target.R})" : "목표 미지정";

    private void BuildNavalCommandDetails(VBoxContainer parent)
    {
        if (_depSelectedUnit < 0 || _depSelectedUnit >= _pendingNavalDeploys.Count)
        {
            var empty = MakeLabel("왼쪽에서 예약 출항부대를 선택하세요.", 16, Parchment);
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.VerticalAlignment = VerticalAlignment.Center;
            empty.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            parent.AddChild(empty);
            return;
        }
        var index = _depSelectedUnit;
        var request = _pendingNavalDeploys[index].Req;
        var leader = _state.Generals.First(general => general.Id == request.Vanguard);
        var ship = _troops.FirstOrDefault(item => item.Code == request.ShipCode)?.Name ?? request.ShipCode;
        var source = _troops.FirstOrDefault(item => item.Code == request.SourceTroopCode)?.Name ?? request.SourceTroopCode;
        var overview = new PanelContainer();
        overview.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 5, 10));
        var overviewRow = new HBoxContainer();
        overviewRow.AddThemeConstantOverride("separation", 12);
        overviewRow.AddChild(DeployUnitArtwork(UnitCard(request.ShipCode, TroopClass.Naval), 76));
        var overviewText = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        overviewText.AddChild(MakeLabel($"{ship}  {request.Troops:N0}명", 21, GoldBright));
        overviewText.AddChild(MakeLabel($"승선 병종 {source}", 13, Parchment));
        overviewRow.AddChild(overviewText);
        overview.AddChild(overviewRow);
        parent.AddChild(overview);
        var officers = new PanelContainer();
        officers.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 5, 10));
        var adjutant = request.Adjutant is { } adj
            ? _state.Generals.FirstOrDefault(general => general.Id == adj)?.Name ?? "-" : "없음";
        officers.AddChild(MakeLabel($"편성 장수\n선봉 {leader.Name} · 무 {leader.Might} · 지 {leader.Intellect} · 정 {leader.Politics}\n부장 {adjutant}", 14, Parchment));
        parent.AddChild(officers);
        var cargo = new PanelContainer();
        cargo.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 5, 10));
        cargo.AddChild(MakeLabel($"보급 현황\n휴대 군량 {request.Provisions:N0} · 휴대 금 {request.Gold:N0}", 14, Parchment));
        parent.AddChild(cargo);
        var command = new PanelContainer();
        command.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 5, 10));
        var commandBox = new VBoxContainer();
        commandBox.AddThemeConstantOverride("separation", 6);
        commandBox.AddChild(MakeLabel($"현재 목표: {NavalTargetName(request)} · {DelayLabel(request.DelayDays)}", 15, GoldBright));


        var target = CommandCenterButton("목표 지정", true, new Vector2(96, 38));
        target.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        target.Pressed += () => BeginNavalTargeting(index);


        commandBox.AddChild(target);
        command.AddChild(commandBox);
        parent.AddChild(command);
        var summary = new PanelContainer { CustomMinimumSize = new Vector2(0, 42) };
        summary.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 4, 10));
        summary.AddChild(MakeLabel($"편성 요약 · {ship} {request.Troops:N0}명 | 선봉 {leader.Name} | {NavalTargetName(request)}", 14, GoldBright));
        parent.AddChild(summary);
    }

    private async void RunNavalCommandCenterQa()
    {
        var argument = OS.GetCmdlineUserArgs().FirstOrDefault(value => value.StartsWith("--maptestnavalcommandcenterqa=", StringComparison.Ordinal));
        if (argument is not null)
        {
            var dimensions = argument.Split('=', 2)[1].Split('x', 2);
            if (dimensions.Length == 2 && int.TryParse(dimensions[0], out var width)
                && int.TryParse(dimensions[1], out var height)) GetWindow().Size = new Vector2I(width, height);
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var port = _state.Cities.First(city => city.Owner == Player && city.IsPort);
        OpenNavalHub(port.Id);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var frame = _modalLayer?.FindChild("NavalCommandCenter", true, false) as DeploymentCommandFrame;
        var passed = frame is not null && GetViewport().GetVisibleRect().Encloses(frame.GetGlobalRect())
            && frame.BodyColumnCount() == 2 && frame.NewButton.Text.Contains("신규 편성", StringComparison.Ordinal);
        GD.Print($"[naval-command-center-qa] passed={passed} size={frame?.Size}");
        CloseModal();
        GetTree().Quit(passed ? 0 : 1);
    }
}
