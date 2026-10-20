using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private bool _generalTransferTargeting;
    private CityId _generalTransferOrigin;
    private GeneralId[] _generalTransferOfficers = [];
    private int _generalTransferCommandIndex;
    private MapSelectionFogOverlay? _generalTransferFog;
    private CanvasLayer? _generalTransferHintLayer;

    private void BuildGeneralTransferSelection(VBoxContainer box, City city)
    {
        var scaffold = _modalLayer?.FindChild("AdaptiveModalScaffold", true, false) as AdaptiveModalScaffold;
        if (scaffold is null) return;
        scaffold.BodyScroll.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
        box.AddChild(MakeLabel("이동할 장수를 체크한 뒤 지도에서 같은 세력의 성·항구를 선택하세요.", 14, Parchment));
        if (!_state.Cities.Any(candidate => candidate.Owner == city.Owner && candidate.Id != city.Id))
        {
            box.AddChild(MakeLabel("이동 가능한 다른 성·항구가 없습니다.", 15, Parchment));
            return;
        }
        var ruler = _state.Factions.FirstOrDefault(faction => faction.Id == city.Owner)?.Ruler;
        var officers = _state.GeneralsAt(city.Id)
            .Select(id => _state.Generals.FirstOrDefault(general => general.Id == id))
            .Where(general => general is not null && general.Id != ruler && !_state.IsGeneralBusy(general.Id))
            .OrderBy(general => general!.Name).ToArray();
        if (officers.Length == 0)
        {
            box.AddChild(MakeLabel("현재 이동할 수 있는 장수가 없습니다.", 15, Parchment));
            return;
        }
        var search = new LineEdit { Name = "GeneralTransferSearch", PlaceholderText = "장수 이름 검색",
            MaxLength = 10, CustomMinimumSize = new Vector2(0, 38) };
        search.AddThemeFontOverride("font", _font);
        box.AddChild(search);
        var scroll = new ScrollContainer { Name = "GeneralTransferOfficerScroll",
            CustomMinimumSize = new Vector2(0, Mathf.Max(160, scaffold.LayoutSize.Y - 180f)),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        box.AddChild(scroll);
        var rows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        rows.AddThemeConstantOverride("separation", 5);
        scroll.AddChild(rows);
        var selected = new HashSet<GeneralId>();
        var renderedRows = new List<(Control Row, string Name)>();
        foreach (var general in officers)
        {
            var officer = general!;
            var row = new HBoxContainer { Name = $"TransferOfficer_{officer.Id.Value}",
                CustomMinimumSize = new Vector2(0, 62) };
            row.AddThemeConstantOverride("separation", 12);
            rows.AddChild(row);
            var check = new CheckBox { Name = $"TransferCheck_{officer.Id.Value}",
                CustomMinimumSize = new Vector2(36, 0) };
            check.Toggled += on => { if (on) selected.Add(officer.Id); else selected.Remove(officer.Id); };
            row.AddChild(check);
            row.AddChild(new TextureRect { Texture = CircularPortraitFor(officer.Id),
                CustomMinimumSize = new Vector2(52, 52),
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize });
            var role = city.Governor == officer.Id ? "태수" : city.Strategist == officer.Id ? "군사" : "대기";
            var label = MakeLabel($"{officer.Name}  ·  {role}", 16, Parchment);
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(label);
            renderedRows.Add((row, officer.Name));
        }
        search.TextChanged += term =>
        {
            foreach (var (row, name) in renderedRows)
                row.Visible = name.Contains(term, StringComparison.OrdinalIgnoreCase);
        };
        var hint = MakeLabel("이동할 장수를 선택하세요.", 13, Parchment);
        scaffold.AddFooterControl(hint);
        var choose = MakeButton("지도에서 이동 지역 선택", accent: true);
        choose.Pressed += () =>
        {
            if (selected.Count == 0) { hint.Text = "장수를 1명 이상 체크하세요."; return; }
            BeginGeneralTransferTargeting(city.Id, selected.OrderBy(id => id.Value).ToArray());
        };
        scaffold.AddFooterControl(choose);
    }

    private void BeginGeneralTransferTargeting(CityId origin, GeneralId[] officers)
    {
        var city = _state.Cities.FirstOrDefault(candidate => candidate.Id == origin && candidate.Owner == Player);
        if (city is null || officers.Length == 0) return;
        _generalTransferOrigin = origin;
        _generalTransferOfficers = officers;
        _generalTransferCommandIndex = _cmdIndex;
        CloseModal();
        HidePanels();
        _generalTransferTargeting = true;
        _generalTransferFog = new MapSelectionFogOverlay();
        AddChild(_generalTransferFog);
        RefreshGeneralTransferFog();
        _generalTransferHintLayer = new CanvasLayer { Layer = 18 };
        AddChild(_generalTransferHintLayer);
        var hint = MakeLabel("원래 색으로 보이는 아군 성·항구를 클릭하세요. Esc 또는 우클릭으로 취소", 16, GoldBright);
        hint.Name = "GeneralTransferMapHint";
        hint.MouseFilter = Control.MouseFilterEnum.Ignore;
        hint.Position = new Vector2(24, 105);
        _generalTransferHintLayer.AddChild(hint);
    }

    private void RefreshGeneralTransferFog()
    {
        if (!_generalTransferTargeting || _generalTransferFog is null) return;
        var owner = _state.Cities.FirstOrDefault(city => city.Id == _generalTransferOrigin)?.Owner;
        var openings = _state.Cities.Where(city => city.Owner == owner && city.Id != _generalTransferOrigin)
            .Select(city =>
            {
                var point = _camera.UnprojectPosition(CastleVisualCenter(city)
                    + new Vector3(0f, _view.TileTopY + .12f, 0f));
                var radius = city.Castle switch
                {
                    CastleSize.Large => 135f,
                    CastleSize.Medium => 110f,
                    _ => 80f,
                };
                return new Vector3(point.X, point.Y, radius);
            }).ToArray();
        _generalTransferFog.SetOpenings(openings);
    }

    private void SelectGeneralTransferDestination(Vector2 screenPosition)
    {
        var hex = RayToGround(screenPosition);
        var destination = hex is { } tile ? CityAtHex(tile) : null;
        var origin = _state.Cities.FirstOrDefault(city => city.Id == _generalTransferOrigin);
        if (origin is null || destination is null || destination.Owner != origin.Owner || destination.Id == origin.Id)
        {
            if (_generalTransferHintLayer?.FindChild("GeneralTransferMapHint", true, false) is Label hint)
                hint.Text = "이동 가능한 아군 성·항구를 선택하세요.";
            return;
        }
        var officerIds = _generalTransferOfficers.ToArray();
        StopGeneralTransferTargeting(false);
        var names = officerIds.Select(id => _state.Generals.First(general => general.Id == id).Name).ToArray();
        var days = Math.Max(1, (origin.Position.Distance(destination.Position) + 1) / 2);
        var released = officerIds.Where(id => origin.Governor == id || origin.Strategist == id).ToArray();
        var releaseText = released.Length == 0 ? "" : "\n태수·군사 직책은 출발 시 해제됩니다.";
        ShowOfficerConfirm("장수이동", $"{string.Join(", ", names)} 장수를 {destination.Name}(으)로 보냅니다.\n예상 소요일 {days}일.{releaseText}",
            officerIds[0], () =>
            {
                var next = _state;
                foreach (var officer in officerIds)
                {
                    var result = _commander.Issue(next, new CommandRequest(origin.Id,
                        CommandKind.TransferGeneral, officer, TargetCity: destination.Id));
                    if (!result.Ok) { ShowNotice("장수이동 불가", result.Error ?? "이동할 수 없습니다."); return; }
                    next = result.State;
                }
                _state = next;
                Report($"[인재] {string.Join(", ", names)} 장수가 {destination.Name}(으)로 이동 중입니다 ({days}일).", GoldBright);
                Redraw($"{officerIds.Length}명 장수이동");
            }, $"{destination.Name}(으)로 이동하겠습니다.");
    }

    private void StopGeneralTransferTargeting(bool reopen)
    {
        _generalTransferTargeting = false;
        if (_generalTransferFog is not null && IsInstanceValid(_generalTransferFog)) _generalTransferFog.QueueFree();
        if (_generalTransferHintLayer is not null && IsInstanceValid(_generalTransferHintLayer)) _generalTransferHintLayer.QueueFree();
        _generalTransferFog = null;
        _generalTransferHintLayer = null;
        _generalTransferOfficers = [];
        if (reopen)
        {
            SelectCity(_generalTransferOrigin);
            OpenModal(_generalTransferCommandIndex);
        }
    }

    private void RunGeneralTransferUiQa()
    {
        var origin = _state.Cities.First(city => city.Owner == Player
            && _state.Cities.Any(other => other.Owner == city.Owner && other.Id != city.Id)
            && _state.GeneralsAt(city.Id).Any(id => !_state.IsGeneralBusy(id)));
        SelectCity(origin.Id);
        var commandIndex = Array.FindIndex(Cmds, command => command.Param == "transfer");
        OpenModal(commandIndex);
        var search = _modalLayer?.FindChild("GeneralTransferSearch", true, false) as LineEdit;
        var list = _modalLayer?.FindChild("GeneralTransferOfficerScroll", true, false) as ScrollContainer;
        var boxes = _modalLayer?.FindChildren("TransferCheck_*", "CheckBox", true, false).OfType<CheckBox>().ToArray() ?? [];
        var layout = search?.MaxLength == 10 && list is not null && boxes.Length > 0
            && list.CustomMinimumSize.Y >= 0.65f * (_modalLayer?.FindChild("AdaptiveModalScaffold", true, false)
                as AdaptiveModalScaffold)?.LayoutSize.Y;
        search!.Text = "절대없는장수";
        search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
        var filtered = boxes.All(check => check.GetParent() is Control row && !row.Visible);
        search.Text = "";
        search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
        boxes[0].ButtonPressed = true;
        if (boxes.Length > 1) boxes[1].ButtonPressed = true;
        var choose = _modalLayer?.FindChildren("*", "Button", true, false).OfType<Button>()
            .FirstOrDefault(button => button.Text == "지도에서 이동 지역 선택");
        choose?.EmitSignal(Button.SignalName.Pressed);
        var mapMode = _generalTransferTargeting && _generalTransferFog is not null
            && _generalTransferFog.GetMeta("opening_count").AsInt32() > 0;
        var destination = _state.Cities.First(city => city.Owner == origin.Owner && city.Id != origin.Id);
        var point = _camera.UnprojectPosition(CastleVisualCenter(destination)
            + new Vector3(0f, _view.TileTopY + .1f, 0f));
        SelectGeneralTransferDestination(point);
        var confirmedTarget = !_generalTransferTargeting && _confirmLayer is not null;
        var accept = _confirmLayer?.FindChildren("*", "Button", true, false).OfType<Button>()
            .FirstOrDefault(button => button.Text == "확인");
        accept?.EmitSignal(Button.SignalName.Pressed);
        var issued = _state.Commands.Count(command => command.Kind == CommandKind.TransferGeneral
            && command.TargetCity == destination.Id) == Math.Min(2, boxes.Length);
        var passed = layout && filtered && mapMode && confirmedTarget && issued;
        GD.Print($"[general-transfer-ui-qa] passed={passed} layout={layout} filtered={filtered} mapMode={mapMode} target={confirmedTarget} issued={issued} officers={boxes.Length}");
        GetTree().Quit(passed ? 0 : 1);
    }
}
