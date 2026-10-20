using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Simulation.RenewalMovement;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private int _armyDragCandidateId = -1;
    private bool _armyDragActive;
    private ContinuousPosition? _armyDragPreviewPosition;

    private void BeginArmyDragCandidate(Vector2 screen)
    {
        _armyDragCandidateId = -1;
        if (_advancing || _depTargeting || _generalTransferTargeting || _stratMapTargeting
            || _fieldBuildPlacing || _placing || _supplyFollowSourceId >= 0
            || _supplyReplenishmentSourceId >= 0 || IsModalBlockingMapNavigation()) return;

        var candidate = DisplayedArmies.Where(unit => unit.Field.Owner == Player && CanSeeUnit(unit)
                && _armyTokens.TryGetValue(unit.Id.Value, out var token)
                && IsInstanceValid(token) && token.Visible)
            .Select(unit => (Unit: unit, Distance: _camera.UnprojectPosition(
                _armyTokens[unit.Id.Value].GlobalPosition + new Vector3(0f, .2f, 0f)).DistanceTo(screen)))
            .Where(entry => entry.Distance <= 28f)
            .OrderBy(entry => entry.Distance).ThenBy(entry => entry.Unit.Id.Value)
            .FirstOrDefault();
        if (candidate.Unit is not null) _armyDragCandidateId = candidate.Unit.Id.Value;
    }

    private bool UpdateArmyDrag(Vector2 screen)
    {
        if (_armyDragCandidateId < 0 || !_leftDown) return false;
        if (!_armyDragActive && screen.DistanceTo(_leftDownPos) < 8f) return false;
        _armyDragActive = true;
        _hover.Visible = false;
        var unit = _state.Armies.FirstOrDefault(candidate => candidate.Id.Value == _armyDragCandidateId);
        var ground = RayToGroundPoint(screen);
        if (unit is null || ground is null) return true;
        if (_armyDragPreviewPosition == ground.Value.Position) return true;
        _armyDragPreviewPosition = ground.Value.Position;
        ClearPathMarkers();
        AddPathDots(unit.Field.Position, ground.Value.Hex, _pathMarkers,
            unit.Class == TroopClass.Naval, ground.Value.Position, unit.RenewalPosition);
        return true;
    }

    private bool FinishArmyDrag(Vector2 screen)
    {
        var id = _armyDragCandidateId;
        var active = _armyDragActive;
        _armyDragCandidateId = -1;
        _armyDragActive = false;
        _armyDragPreviewPosition = null;
        if (!active) return false;

        var unit = _state.Armies.FirstOrDefault(candidate => candidate.Id.Value == id
            && candidate.Field.Owner == Player && candidate.Pool.Active > 0);
        var ground = RayToGroundPoint(screen);
        if (unit is null || ground is null || unit.Field.Position == ground.Value.Hex
            && unit.RenewalPosition == ground.Value.Position)
        {
            ClearPathMarkers();
            return true;
        }
        _targetingNavalUnit = unit.Class == TroopClass.Naval;
        if (!HasPath(unit.Field.Position, ground.Value.Hex))
        {
            ClearPathMarkers();
            ShowNotice("이동 불가", "선택한 지점까지 이동 가능한 경로가 없습니다.");
            return true;
        }
        ShowArmyDragModePicker(unit, ground.Value.Hex, ground.Value.Position);
        return true;
    }

    private void CancelArmyDrag()
    {
        _armyDragCandidateId = -1;
        _armyDragActive = false;
        _armyDragPreviewPosition = null;
        _leftDown = false;
        ClearPathMarkers();
    }

    private void ShowArmyDragModePicker(CombatUnit unit, HexCoord target, ContinuousPosition exactTarget)
    {
        FadeOutModal(_confirmLayer);
        var layer = new CanvasLayer { Name = "ArmyDragModePicker", Layer = 40 };
        AddChild(layer);
        _confirmLayer = layer;
        void Close()
        {
            FadeOutModal(layer);
            if (_confirmLayer == layer) _confirmLayer = null;
            ClearPathMarkers();
        }

        var backdrop = new ColorRect { Color = new Color(0, 0, 0, .45f),
            MouseFilter = Control.MouseFilterEnum.Stop };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        backdrop.GuiInput += input =>
        {
            if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) Close();
        };
        layer.AddChild(backdrop);
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(center);
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        panel.AddThemeStyleboxOverride("panel", Frame(Ink, Gold, 2, 10, 14));
        center.AddChild(panel);
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(320, 0) };
        box.AddThemeConstantOverride("separation", 8);
        panel.AddChild(box);
        box.AddChild(MakeLabel("이동 방식 선택", 18, GoldBright));
        box.AddChild(MakeLabel("드래그한 지점까지의 경로를 확정합니다.", 13, Parchment));
        foreach (var mode in new[] { UnitMode.March, UnitMode.Advance, UnitMode.Attack })
        {
            var choice = MakeButton(ModeName(mode), accent: mode == UnitMode.March);
            choice.Name = $"ArmyDragMode_{mode}";
            choice.Pressed += () =>
            {
                Close();
                _retargetUnitId = unit.Id.Value;
                _retargetMode = mode;
                _targetingNavalUnit = unit.Class == TroopClass.Naval;
                ApplyUnitTarget(target, null, exactTarget, null);
            };
            box.AddChild(choice);
        }
        var cancel = MakeButton("취소");
        cancel.Pressed += Close;
        box.AddChild(cancel);
    }

    private void RunArmyDragQa()
    {
        var start = _map.Tiles().FirstOrDefault(position =>
            _passability.CanEnter(MovementDomain.Land, position)
            && position.Neighbors().Any(neighbor => _map.Contains(neighbor)
                && _passability.CanEnter(MovementDomain.Land, neighbor)));
        var destination = start.Neighbors().First(neighbor => _map.Contains(neighbor)
            && _passability.CanEnter(MovementDomain.Land, neighbor));
        var officer = _state.Generals.First(general => _state.PostingOf(general.Id)?.Faction == Player);
        var field = new FieldUnit(new UnitId(99071), Player, start, 2, 1, 1,
            MovementDomain.Land, UnitMode.Standby, null, 99071, 1);
        var sample = new CombatUnit(field, new CombatStats(1000, 10, 10),
            new TroopPool(1000, 0), UnitCombatState.Create(60), VanguardId: officer.Id,
            RenewalPosition: RenewalHexSpace.Center(start));
        _state = _state with { FieldArmies = _state.Armies.Append(sample).ToList() };
        Redraw("부대 드래그 QA");
        var unit = _state.Armies.FirstOrDefault(candidate => candidate.Id == sample.Id
            && _armyTokens.TryGetValue(candidate.Id.Value, out var token) && token.Visible);
        if (unit is null)
        {
            GD.Print("[army-drag-qa] passed=False reason=no-player-unit");
            GetTree().Quit(1);
            return;
        }
        var screen = _camera.UnprojectPosition(_armyTokens[unit.Id.Value].GlobalPosition
            + new Vector3(0f, .2f, 0f));
        BeginArmyDragCandidate(screen);
        var selected = _armyDragCandidateId == unit.Id.Value;
        var target = RenewalHexSpace.Center(destination);
        _leftDown = true;
        _leftDownPos = screen;
        var destinationScreen = _camera.UnprojectPosition(ContinuousToWorld(target));
        UpdateArmyDrag(destinationScreen);
        _leftDown = false;
        var preview = _armyDragActive && _pathMarkers.Count > 0;
        FinishArmyDrag(destinationScreen);
        var choices = _confirmLayer?.FindChildren("ArmyDragMode_*", "Button", true, false)
            .OfType<Button>().ToArray() ?? [];
        var modes = choices.Select(button => button.Text).ToArray();
        choices.FirstOrDefault(button => button.Text == "전진")?.EmitSignal(Button.SignalName.Pressed);
        var ordered = _state.Armies.First(unit => unit.Id == sample.Id);
        var issued = ordered.Field.Mode == UnitMode.Advance && ordered.Field.Target == destination;
        var passed = selected && preview && modes.SequenceEqual(new[] { "행군", "전진", "공격" }) && issued;
        GD.Print($"[army-drag-qa] passed={passed} selected={selected} preview={preview} modes={string.Join(',', modes)} issued={issued}");
        GetTree().Quit(passed ? 0 : 1);
    }
}
