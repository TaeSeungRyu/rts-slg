using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    // 수송 지휘소 전용 디자인 값입니다. 크기와 간격의 단위는 px입니다.
    private static class TransportHubLayout
    {
        public const int ModalLayer = 20;
        public const string PanelTexture = "deploy_command_panel.png";
        public static readonly Color BackdropColor = new(0, 0, 0, 0.68f);

        public static class Card
        {
            public static readonly Vector2 MinimumSize = new(377, 112);
            public const int Padding = 18;
            public const int Gap = 12;
            public const int ArtworkSize = 68;
            public const int TitleFontSize = 17;
            public const int LeaderFontSize = 13;
            public const int DestinationFontSize = 12;
            public const int EmptyFontSize = 15;
            public const string NormalTexture = "troop_normal.png";
            public const string ActiveTexture = "troop_hover_active.png";
        }

        public static class Details
        {
            public const int OverviewGap = 12;
            public const int ArtworkSize = 76;
            public const int TitleFontSize = 21;
            public const int RosterFontSize = 13;
            public const int BodyFontSize = 14;
            public const int DestinationFontSize = 15;
            public const int EmptyFontSize = 16;
            public const int SummaryFontSize = 14;
            public const int SummaryHeight = 42;
            public const int SummaryCornerRadius = 4;
            public const int BorderWidth = 1;
            public const int CornerRadius = 5;
            public const int Padding = 10;
            public static Color PanelBackground => Colors.Transparent;
            public static Color PanelBorder => Gold;
        }

        public static class Buttons
        {
            public static readonly Vector2 CloseSize = new(30, 30);
            public const int CloseFontSize = 18;
            public const int CloseBorderWidth = 1;
            public const int CloseCornerRadius = 4;
            public const int ClosePadding = 2;
            public static readonly Color CloseHoverBackground = new("5b1712");
            public static readonly Color ClosePressedBackground = new("35100d");

            public static readonly Vector2 DeleteSize = new(132, 42);
            public static readonly Vector2 EditSize = new(145, 42);
            public static readonly Vector2 ConfirmSize = new(190, 42);
            public static readonly Vector2 TargetSize = new(132, 38);
        }

        public static Color HighlightText => GoldBright;
        public static Color BodyText => Parchment;
    }

    private void OpenTransportHub(CityId city)
    {
        _depModalCity = city;
        if (_modalLayer is not null)
        {
            FadeOutModal(_modalLayer);
            _modalLayer = null;
        }

        var layer = new CanvasLayer { Layer = TransportHubLayout.ModalLayer };
        AddChild(layer);
        _modalLayer = layer;
        var backdrop = new ColorRect
        {
            Color = TransportHubLayout.BackdropColor,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(backdrop);
        var mine = Enumerable.Range(0, _pendingTransportDeploys.Count)
            .Where(index => _pendingTransportDeploys[index].Req.City == city).ToList();
        if (!mine.Contains(_depSelectedUnit)) _depSelectedUnit = mine.FirstOrDefault(-1);
        var cityName = _state.Cities.First(item => item.Id == city).Name;
        var frame = new DeploymentCommandFrame(GetViewport().GetVisibleRect().Size,
            GD.Load<Texture2D>(DeployCommandAssetRoot + TransportHubLayout.PanelTexture), _font,
            $"수송 지휘소  《 {cityName} 》", $"예약된 수송부대  {mine.Count}",
            "예약 수송부대 목록", "선택 수송부대 정보");
        frame.Name = "TransportCommandCenter";
        layer.AddChild(frame);
        StyleTransportCommandCloseButton(frame.CloseButton);
        frame.CloseButton.Pressed += () => { CloseModal(); SelectCity(city); };
        ApplyDeployComposeButtonTexture(frame.NewButton, true);
        frame.NewButton.Pressed += () => OpenTransportCompose(city);
        foreach (var index in mine)
            frame.List.AddChild(BuildTransportCommandCard(index, city));
        if (mine.Count == 0)
        {
            frame.List.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

            var empty = MakeLabel(
                "예약된 수송부대가 없습니다.\n신규 편성으로 부대를 추가하세요.",
                TransportHubLayout.Card.EmptyFontSize,
                TransportHubLayout.BodyText);

            empty.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            empty.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.VerticalAlignment = VerticalAlignment.Center;

            frame.List.AddChild(empty);
        }
        BuildTransportCommandDetails(frame.Details);
        BuildTransportCommandActions(frame.Actions, city);
        CenterAndDrag(frame, frame.Header, frame.Size.X, frame.Size.Y, frame.Root);
        frame.Position = (GetViewport().GetVisibleRect().Size - frame.Size) / 2f;
    }

    private void StyleTransportCommandCloseButton(Button close)
    {
        close.Text = "×";
        close.CustomMinimumSize = TransportHubLayout.Buttons.CloseSize;
        close.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        close.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        close.AddThemeFontSizeOverride("font_size", TransportHubLayout.Buttons.CloseFontSize);

        var borderWidth = TransportHubLayout.Buttons.CloseBorderWidth;
        var cornerRadius = TransportHubLayout.Buttons.CloseCornerRadius;
        var padding = TransportHubLayout.Buttons.ClosePadding;
        close.AddThemeStyleboxOverride("normal", Frame(Colors.Transparent, Gold, borderWidth, cornerRadius, padding));
        close.AddThemeStyleboxOverride("hover",
            Frame(TransportHubLayout.Buttons.CloseHoverBackground, GoldBright, borderWidth, cornerRadius, padding));
        close.AddThemeStyleboxOverride("pressed",
            Frame(TransportHubLayout.Buttons.ClosePressedBackground, GoldBright, borderWidth, cornerRadius, padding));
        close.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
    }

    private void BuildTransportCommandActions(Control parent, CityId city)
    {
        if (_depSelectedUnit >= 0 && _depSelectedUnit < _pendingTransportDeploys.Count)
        {
            var selected = _depSelectedUnit;
            var remove = CommandCenterButton("부대 삭제", false, TransportHubLayout.Buttons.DeleteSize);
            remove.Pressed += () =>
            {
                _pendingTransportDeploys.RemoveAt(selected);
                _depSelectedUnit = -1;
                OpenTransportHub(city);
            };
            parent.AddChild(remove);
            var edit = CommandCenterButton("편성 수정", false, TransportHubLayout.Buttons.EditSize);
            edit.Pressed += () => OpenTransportCompose(city);
            parent.AddChild(edit);
        }
        parent.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var done = CommandCenterButton("확인", true, TransportHubLayout.Buttons.ConfirmSize);
        done.Pressed += () => { CloseModal(); SelectCity(city); };
        parent.AddChild(done);
    }

    private Control BuildTransportCommandCard(int index, CityId city)
    {
        var request = _pendingTransportDeploys[index].Req;
        var total = request.Lines.Sum(line => line.Troops);
        var leader = _state.Generals.FirstOrDefault(general => general.Id == request.Vanguard)?.Name ?? "-";
        var button = new Button
        {
            Name = $"TransportCommandCard{index}",
            Text = "",
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = TransportHubLayout.Card.MinimumSize,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
        };
        var activeTexture = GD.Load<Texture2D>(DeployCommandAssetRoot + TransportHubLayout.Card.ActiveTexture);
        var normalTexture = index == _depSelectedUnit
            ? activeTexture
            : GD.Load<Texture2D>(DeployCommandAssetRoot + TransportHubLayout.Card.NormalTexture);
        button.AddThemeStyleboxOverride("normal", CommandCenterTextureBox(normalTexture));
        var active = CommandCenterTextureBox(activeTexture);
        button.AddThemeStyleboxOverride("hover", active);
        button.AddThemeStyleboxOverride("pressed", active);
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.Pressed += () => { _depSelectedUnit = index; OpenTransportHub(city); };
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.Minsize, TransportHubLayout.Card.Padding);
        row.OffsetLeft += 30; // 왼쪽 여백만 추가
        row.AddThemeConstantOverride("separation", TransportHubLayout.Card.Gap);
        var artworkSize = TransportHubLayout.Card.ArtworkSize;
        var art = FixedIcon(UnitCard("transport"), artworkSize);
        art.CustomMinimumSize = new Vector2(artworkSize, artworkSize);
        art.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        art.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.AddChild(art);
        var info = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        info.AddChild(MakeLabel($"수송부대  {total:N0}명", TransportHubLayout.Card.TitleFontSize, TransportHubLayout.HighlightText));
        info.AddChild(MakeLabel($"주장 {leader}", TransportHubLayout.Card.LeaderFontSize, TransportHubLayout.BodyText));
        info.AddChild(MakeLabel(TransportDestination(request), TransportHubLayout.Card.DestinationFontSize, TransportHubLayout.HighlightText));
        row.AddChild(info);
        button.AddChild(row);
        return button;
    }

    private string TransportDestination(TransportDeployRequest request)
        => request.Destination is { } id
            ? _state.Cities.FirstOrDefault(city => city.Id == id)?.Name ?? $"성{id.Value}"
            : "목표 미지정";

    private void BuildTransportCommandDetails(VBoxContainer parent)
    {
        if (_depSelectedUnit < 0 || _depSelectedUnit >= _pendingTransportDeploys.Count)
        {
            var empty = MakeLabel(
                "왼쪽에서 예약 수송부대를 선택하세요.",
                TransportHubLayout.Details.EmptyFontSize,
                TransportHubLayout.BodyText);

            empty.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            empty.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.VerticalAlignment = VerticalAlignment.Center;
            parent.AddChild(empty);
            return;
        }
        var index = _depSelectedUnit;
        var request = _pendingTransportDeploys[index].Req;
        var total = request.Lines.Sum(line => line.Troops);
        var leader = _state.Generals.First(general => general.Id == request.Vanguard);
        var roster = string.Join(" · ", request.Lines.Select(line =>
            $"{_troops.FirstOrDefault(troop => troop.Code == line.TroopCode)?.Name ?? line.TroopCode} {line.Troops:N0}"));
        var destinationName = TransportDestination(request);

        var overviewRow = new HBoxContainer();
        overviewRow.AddThemeConstantOverride("separation", TransportHubLayout.Details.OverviewGap);
        overviewRow.AddChild(DeployUnitArtwork(UnitCard("transport"), TransportHubLayout.Details.ArtworkSize));
        var overviewText = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        overviewText.AddChild(MakeLabel($"수송부대  {total:N0}명",
            TransportHubLayout.Details.TitleFontSize, TransportHubLayout.HighlightText));
        overviewText.AddChild(MakeLabel(roster, TransportHubLayout.Details.RosterFontSize, TransportHubLayout.BodyText));
        overviewRow.AddChild(overviewText);
        parent.AddChild(BuildTransportDetailPanel(overviewRow));

        var officer = MakeLabel($"편성 장수\n주장 {leader.Name} · 무 {leader.Might} · 지 {leader.Intellect} · 정 {leader.Politics}",
            TransportHubLayout.Details.BodyFontSize, TransportHubLayout.BodyText);
        parent.AddChild(BuildTransportDetailPanel(officer));

        var cargo = MakeLabel($"수송 화물\n군량 {request.Provisions:N0} · 금 {request.Gold:N0}",
            TransportHubLayout.Details.BodyFontSize, TransportHubLayout.BodyText);
        parent.AddChild(BuildTransportDetailPanel(cargo));

        var destinationBox = new VBoxContainer();
        destinationBox.AddChild(MakeLabel($"목표: {destinationName} · {DelayLabel(request.DelayDays)}",
            TransportHubLayout.Details.DestinationFontSize, TransportHubLayout.HighlightText));
        var target = CommandCenterButton("목표 지정", true, TransportHubLayout.Buttons.TargetSize);
        target.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        target.Pressed += () => BeginTransportTargeting(index);
        destinationBox.AddChild(target);
        parent.AddChild(BuildTransportDetailPanel(destinationBox));

        var summary = MakeLabel($"편성 요약 · 수송 {total:N0}명 | 주장 {leader.Name} | {destinationName}",
            TransportHubLayout.Details.SummaryFontSize, TransportHubLayout.HighlightText);
        parent.AddChild(BuildTransportDetailPanel(summary,
            cornerRadius: TransportHubLayout.Details.SummaryCornerRadius,
            minimumHeight: TransportHubLayout.Details.SummaryHeight));
    }

    private PanelContainer BuildTransportDetailPanel(
        Control content,
        int cornerRadius = TransportHubLayout.Details.CornerRadius,
        int minimumHeight = 0)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(0, minimumHeight) };
        panel.AddThemeStyleboxOverride("panel", Frame(
            TransportHubLayout.Details.PanelBackground,
            TransportHubLayout.Details.PanelBorder,
            TransportHubLayout.Details.BorderWidth,
            cornerRadius,
            TransportHubLayout.Details.Padding));
        panel.AddChild(content);
        return panel;
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
