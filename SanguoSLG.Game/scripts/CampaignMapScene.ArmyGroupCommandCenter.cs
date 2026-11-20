using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private void OpenArmyGroupHub(CityId city)
    {
        _depModalCity = city;
        if (_modalLayer is not null) { FadeOutModal(_modalLayer); _modalLayer = null; }
        var layer = new CanvasLayer { Layer = 20 };
        AddChild(layer);
        _modalLayer = layer;
        var backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.68f), MouseFilter = Control.MouseFilterEnum.Stop };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(backdrop);
        var mine = Enumerable.Range(0, _pendingArmyGroupDeploys.Count)
            .Where(index => _pendingArmyGroupDeploys[index].Req.City == city).ToList();
        if (!mine.Contains(_depSelectedUnit)) _depSelectedUnit = mine.FirstOrDefault(-1);
        var existing = _state.Armies.Any(unit => unit.IsArmyGroup && unit.OriginCity == city);
        var cityName = _state.Cities.First(item => item.Id == city).Name;
        var frame = new DeploymentCommandFrame(GetViewport().GetVisibleRect().Size,
            GD.Load<Texture2D>(DeployCommandAssetRoot + "deploy_command_panel.png"), _font,
            $"집단군 지휘소  《 {cityName} 》", $"예약된 집단군  {mine.Count} · 성 원점 기준 최대 1개",
            "예약 집단군 목록", "선택 집단군 정보");
        frame.Name = "ArmyGroupCommandCenter";
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
        close.Pressed += () => { CloseModal(); SelectCity(city); };

        ApplyDeployComposeButtonTexture(frame.NewButton, true);
        frame.NewButton.Disabled = existing || mine.Count > 0;
        frame.NewButton.Text = existing ? "출전중" : mine.Count > 0 ? "예약됨" : "＋ 신규 편성";
        frame.NewButton.Pressed += () => OpenArmyGroupCompose(-1);
        foreach (var index in mine) frame.List.AddChild(BuildArmyGroupCommandCard(index, city));
        if (mine.Count == 0) frame.List.AddChild(MakeLabel(existing
            ? "집단군이 이미 출전 중입니다." : "예약된 집단군이 없습니다.\n신규 편성으로 부대를 추가하세요.", 15, Parchment));
        BuildArmyGroupCommandDetails(frame.Details);
        if (_depSelectedUnit >= 0 && _depSelectedUnit < _pendingArmyGroupDeploys.Count)
        {
            var selected = _depSelectedUnit;
            var remove = CommandCenterButton("부대 삭제", false, new Vector2(132, 42));
            remove.Pressed += () => { _pendingArmyGroupDeploys.RemoveAt(selected); _depSelectedUnit = -1; OpenArmyGroupHub(city); };
            frame.Actions.AddChild(remove);
            var edit = CommandCenterButton("편성 수정", false, new Vector2(145, 42));
            edit.Pressed += () => OpenArmyGroupCompose(selected);
            frame.Actions.AddChild(edit);
        }
        frame.Actions.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var done = CommandCenterButton("확인", true, new Vector2(190, 42));
        done.Pressed += () => { CloseModal(); SelectCity(city); };
        
        frame.Actions.AddChild(done);
        CenterAndDrag(frame, frame.Header, frame.Size.X, frame.Size.Y, frame.Root);
        frame.Position = (GetViewport().GetVisibleRect().Size - frame.Size) / 2f;
    }

    private Control BuildArmyGroupCommandCard(int index, CityId city)
    {
        var request = _pendingArmyGroupDeploys[index].Req;
        var total = request.Lines.Sum(line => line.Troops);
        var leader = _state.Generals.FirstOrDefault(general => general.Id == request.Vanguard)?.Name ?? "-";
        var button = new Button
        {
            Name = $"ArmyGroupCommandCard{index}", Text = "", FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(377, 112), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
        };
        button.AddThemeStyleboxOverride("normal", CommandCenterTextureBox(GD.Load<Texture2D>(DeployCommandAssetRoot
            + (index == _depSelectedUnit ? "troop_hover_active.png" : "troop_normal.png"))));
        var active = CommandCenterTextureBox(GD.Load<Texture2D>(DeployCommandAssetRoot + "troop_hover_active.png"));
        button.AddThemeStyleboxOverride("hover", active);
        button.AddThemeStyleboxOverride("pressed", active);
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.Pressed += () => { _depSelectedUnit = index; OpenArmyGroupHub(city); };
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };


        //row.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.Minsize, 18);

        row.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        row.OffsetLeft = 48;
        // row.OffsetRight = -24;
        // row.OffsetTop = 180;
        // row.OffsetBottom = -12;

        row.AddThemeConstantOverride("separation", 12);
        var art = FixedIcon(ArmyGroupIcon(), 68);
        art.CustomMinimumSize = new Vector2(68, 68);
        art.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        art.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.AddChild(art);
        var info = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        info.AddChild(MakeLabel($"집단군  {total:N0}명", 17, GoldBright));
        info.AddChild(MakeLabel($"선봉 {leader}", 13, Parchment));
        info.AddChild(MakeLabel(ArmyGroupTargetName(request.Target), 12, GoldBright));
        row.AddChild(info);
        button.AddChild(row);
        return button;
    }

    private string ArmyGroupTargetName(HexCoord? target)
        => target is { } hex
            ? _state.Cities.FirstOrDefault(city => city.Position == hex)?.Name ?? $"({hex.Q},{hex.R})"
            : "목표 미지정";

    private void BuildArmyGroupCommandDetails(VBoxContainer parent)
    {
        if (_depSelectedUnit < 0 || _depSelectedUnit >= _pendingArmyGroupDeploys.Count)
        {
            var empty = MakeLabel("왼쪽에서 예약 집단군을 선택하세요.", 16, Parchment);
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            parent.AddChild(empty);
            return;
        }
        var index = _depSelectedUnit;
        var request = _pendingArmyGroupDeploys[index].Req;
        var total = request.Lines.Sum(line => line.Troops);
        var leader = _state.Generals.First(general => general.Id == request.Vanguard);
        var overview = new PanelContainer();
        overview.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 5, 10));
        var overviewRow = new HBoxContainer();
        overviewRow.AddThemeConstantOverride("separation", 12);
        overviewRow.AddChild(DeployUnitArtwork(ArmyGroupIcon(), 76));
        var overviewText = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        overviewText.AddChild(MakeLabel($"집단군  {total:N0}명", 21, GoldBright));
        overviewText.AddChild(MakeLabel(ArmyGroupLineText(request.Lines), 13, Parchment));
        overviewRow.AddChild(overviewText);
        overview.AddChild(overviewRow);
        parent.AddChild(overview);
        var officer = new PanelContainer();
        officer.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 5, 10));
        officer.AddChild(MakeLabel($"편성 장수\n선봉 {leader.Name} · 무 {leader.Might} · 지 {leader.Intellect} · 정 {leader.Politics}", 14, Parchment));
        parent.AddChild(officer);
        var supply = new PanelContainer();
        supply.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 5, 10));
        supply.AddChild(MakeLabel($"보급 현황\n휴대 군량 {request.Provisions:N0} · 휴대 금 {request.Gold:N0}", 14, Parchment));
        parent.AddChild(supply);
        var command = new PanelContainer();
        command.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 5, 10));
        var commandBox = new VBoxContainer();
        commandBox.AddChild(MakeLabel($"목표: {ArmyGroupTargetName(request.Target)} · {DelayLabel(request.DelayDays)}", 15, GoldBright));
        var target = CommandCenterButton("목표 지정", true, new Vector2(96, 38));
        target.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        target.Pressed += () => BeginArmyGroupTargeting(index);
        commandBox.AddChild(target);
        command.AddChild(commandBox);
        parent.AddChild(command);
        var summary = new PanelContainer { CustomMinimumSize = new Vector2(0, 42) };
        summary.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 4, 10));
        summary.AddChild(MakeLabel($"편성 요약 · 집단군 {total:N0}명 | 선봉 {leader.Name} | {ArmyGroupTargetName(request.Target)}", 14, GoldBright));
        parent.AddChild(summary);
    }

    private async void RunArmyGroupCommandCenterQa()
    {
        var argument = OS.GetCmdlineUserArgs().FirstOrDefault(value => value.StartsWith("--maptestarmygroupcommandcenterqa=", StringComparison.Ordinal));
        if (argument is not null)
        {
            var dimensions = argument.Split('=', 2)[1].Split('x', 2);
            if (dimensions.Length == 2 && int.TryParse(dimensions[0], out var width)
                && int.TryParse(dimensions[1], out var height)) GetWindow().Size = new Vector2I(width, height);
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var city = _state.Cities.First(candidate => candidate.Owner == Player);
        OpenArmyGroupHub(city.Id);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var panel = _modalLayer?.FindChild("ArmyGroupCommandCenter", true, false) as DeploymentCommandFrame;
        var passed = panel is not null && GetViewport().GetVisibleRect().Encloses(panel.GetGlobalRect())
            && panel.BodyColumnCount() == 2;
        GD.Print($"[army-group-command-center-qa] passed={passed} size={panel?.Size} addDisabled={panel?.NewButton.Disabled}");
        CloseModal();
        GetTree().Quit(passed ? 0 : 1);
    }
}
