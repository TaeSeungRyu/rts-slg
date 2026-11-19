using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private void OpenTransportHub(CityId city)
    {
        _depModalCity = city;
        if (_modalLayer is not null) { FadeOutModal(_modalLayer); _modalLayer = null; }
        var layer = new CanvasLayer { Layer = 20 };
        AddChild(layer);
        _modalLayer = layer;
        var backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.68f), MouseFilter = Control.MouseFilterEnum.Stop };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(backdrop);
        var mine = Enumerable.Range(0, _pendingTransportDeploys.Count)
            .Where(index => _pendingTransportDeploys[index].Req.City == city).ToList();
        if (!mine.Contains(_depSelectedUnit)) _depSelectedUnit = mine.FirstOrDefault(-1);
        var cityName = _state.Cities.First(item => item.Id == city).Name;
        var frame = new DeploymentCommandFrame(GetViewport().GetVisibleRect().Size,
            GD.Load<Texture2D>(DeployCommandAssetRoot + "deploy_command_panel.png"), _font,
            $"수송 지휘소  《 {cityName} 》", $"예약된 수송부대  {mine.Count}",
            "예약 수송부대 목록", "선택 수송부대 정보");
        frame.Name = "TransportCommandCenter";
        layer.AddChild(frame);
        ApplyDeployComposeButtonTexture(frame.CloseButton, false);
        frame.CloseButton.Pressed += () => { CloseModal(); SelectCity(city); };
        ApplyDeployComposeButtonTexture(frame.NewButton, true);
        frame.NewButton.Pressed += () => OpenTransportCompose(city);
        foreach (var index in mine) frame.List.AddChild(BuildTransportCommandCard(index, city));
        if (mine.Count == 0) frame.List.AddChild(MakeLabel("예약된 수송부대가 없습니다.\n신규 편성으로 부대를 추가하세요.", 15, Parchment));
        BuildTransportCommandDetails(frame.Details, city);
        if (_depSelectedUnit >= 0 && _depSelectedUnit < _pendingTransportDeploys.Count)
        {
            var selected = _depSelectedUnit;
            var remove = CommandCenterButton("부대 삭제", false, new Vector2(132, 42));
            remove.Pressed += () => { _pendingTransportDeploys.RemoveAt(selected); _depSelectedUnit = -1; OpenTransportHub(city); };
            frame.Actions.AddChild(remove);
            var edit = CommandCenterButton("편성 수정", false, new Vector2(145, 42));
            edit.Pressed += () => OpenTransportCompose(city);
            frame.Actions.AddChild(edit);
        }
        frame.Actions.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var done = CommandCenterButton("확인", true, new Vector2(190, 42));
        done.Pressed += () => { CloseModal(); SelectCity(city); };
        frame.Actions.AddChild(done);
        CenterAndDrag(frame, frame.Header, frame.Size.X, frame.Size.Y, frame.Root);
        frame.Position = (GetViewport().GetVisibleRect().Size - frame.Size) / 2f;
    }

    private Control BuildTransportCommandCard(int index, CityId city)
    {
        var request = _pendingTransportDeploys[index].Req;
        var total = request.Lines.Sum(line => line.Troops);
        var leader = _state.Generals.FirstOrDefault(general => general.Id == request.Vanguard)?.Name ?? "-";
        var button = new Button
        {
            Name = $"TransportCommandCard{index}", Text = "", FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(377, 112), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
        };
        button.AddThemeStyleboxOverride("normal", CommandCenterTextureBox(GD.Load<Texture2D>(DeployCommandAssetRoot
            + (index == _depSelectedUnit ? "troop_hover_active.png" : "troop_normal.png"))));
        var active = CommandCenterTextureBox(GD.Load<Texture2D>(DeployCommandAssetRoot + "troop_hover_active.png"));
        button.AddThemeStyleboxOverride("hover", active);
        button.AddThemeStyleboxOverride("pressed", active);
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.Pressed += () => { _depSelectedUnit = index; OpenTransportHub(city); };
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.Minsize, 18);
        row.AddThemeConstantOverride("separation", 12);
        var art = FixedIcon(UnitCard("transport"), 68);
        art.CustomMinimumSize = new Vector2(68, 68);
        art.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        art.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.AddChild(art);
        var info = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        info.AddChild(MakeLabel($"수송부대  {total:N0}명", 17, GoldBright));
        info.AddChild(MakeLabel($"주장 {leader}", 13, Parchment));
        info.AddChild(MakeLabel(TransportDestination(request), 12, GoldBright));
        row.AddChild(info);
        button.AddChild(row);
        return button;
    }

    private string TransportDestination(TransportDeployRequest request)
        => request.Destination is { } id
            ? _state.Cities.FirstOrDefault(city => city.Id == id)?.Name ?? $"성{id.Value}"
            : "목표 미지정";

    private void BuildTransportCommandDetails(VBoxContainer parent, CityId city)
    {
        if (_depSelectedUnit < 0 || _depSelectedUnit >= _pendingTransportDeploys.Count)
        {
            var empty = MakeLabel("왼쪽에서 예약 수송부대를 선택하세요.", 16, Parchment);
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            parent.AddChild(empty);
            return;
        }
        var index = _depSelectedUnit;
        var request = _pendingTransportDeploys[index].Req;
        var total = request.Lines.Sum(line => line.Troops);
        var leader = _state.Generals.First(general => general.Id == request.Vanguard);
        var roster = string.Join(" · ", request.Lines.Select(line =>
            $"{_troops.FirstOrDefault(troop => troop.Code == line.TroopCode)?.Name ?? line.TroopCode} {line.Troops:N0}"));
        var overview = new PanelContainer();
        overview.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 5, 10));
        var overviewRow = new HBoxContainer();
        overviewRow.AddThemeConstantOverride("separation", 12);
        overviewRow.AddChild(DeployUnitArtwork(UnitCard("transport"), 76));
        var overviewText = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        overviewText.AddChild(MakeLabel($"수송부대  {total:N0}명", 21, GoldBright));
        overviewText.AddChild(MakeLabel(roster, 13, Parchment));
        overviewRow.AddChild(overviewText);
        overview.AddChild(overviewRow);
        parent.AddChild(overview);
        var officer = new PanelContainer();
        officer.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 5, 10));
        officer.AddChild(MakeLabel($"편성 장수\n주장 {leader.Name} · 무 {leader.Might} · 지 {leader.Intellect} · 정 {leader.Politics}", 14, Parchment));
        parent.AddChild(officer);
        var cargo = new PanelContainer();
        cargo.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 5, 10));
        cargo.AddChild(MakeLabel($"수송 화물\n군량 {request.Provisions:N0} · 금 {request.Gold:N0}", 14, Parchment));
        parent.AddChild(cargo);
        var destination = new PanelContainer();
        destination.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 5, 10));
        var destinationBox = new VBoxContainer();
        destinationBox.AddChild(MakeLabel($"목표: {TransportDestination(request)} · {DelayLabel(request.DelayDays)}", 15, GoldBright));
        var target = CommandCenterButton("목표 지정", true, new Vector2(132, 38));
        target.Pressed += () => BeginTransportTargeting(index);
        destinationBox.AddChild(target);
        destination.AddChild(destinationBox);
        parent.AddChild(destination);
        var summary = new PanelContainer { CustomMinimumSize = new Vector2(0, 42) };
        summary.AddThemeStyleboxOverride("panel", Frame(Colors.Transparent, Gold, 1, 4, 10));
        summary.AddChild(MakeLabel($"편성 요약 · 수송 {total:N0}명 | 주장 {leader.Name} | {TransportDestination(request)}", 14, GoldBright));
        parent.AddChild(summary);
    }

    private async void RunTransportCommandCenterQa()
    {
        var argument = OS.GetCmdlineUserArgs().FirstOrDefault(value => value.StartsWith("--maptesttransportcommandcenterqa=", StringComparison.Ordinal));
        if (argument is not null)
        {
            var dimensions = argument.Split('=', 2)[1].Split('x', 2);
            if (dimensions.Length == 2 && int.TryParse(dimensions[0], out var width)
                && int.TryParse(dimensions[1], out var height)) GetWindow().Size = new Vector2I(width, height);
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var city = _state.Cities.First(candidate => candidate.Owner == Player);
        OpenTransportHub(city.Id);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var panel = _modalLayer?.FindChild("TransportCommandCenter", true, false) as DeploymentCommandFrame;
        var passed = panel is not null && GetViewport().GetVisibleRect().Encloses(panel.GetGlobalRect())
            && panel.BodyColumnCount() == 2 && panel.NewButton.Text.Contains("신규 편성", StringComparison.Ordinal);
        GD.Print($"[transport-command-center-qa] passed={passed} size={panel?.Size}");
        CloseModal();
        GetTree().Quit(passed ? 0 : 1);
    }
}
