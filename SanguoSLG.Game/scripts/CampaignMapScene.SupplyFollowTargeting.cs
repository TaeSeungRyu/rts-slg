using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private MapSelectionFogOverlay? _followSelectionFog;
    private UnitId? _hoveredSupplyFollowTarget;

    private void BeginSupplyFollowTargeting(int sourceId)
    {
        var source = _state.Armies.FirstOrDefault(unit => unit.Id.Value == sourceId
            && unit.Field.Owner == Player && unit.IsSupply && unit.Pool.Active > 0);
        if (_advancing || source is null) return;
        if (!_state.Armies.Any(IsSupplyFollowCandidate))
        {
            ShowNotice("따라가기", "따라갈 아군 야전 전투부대가 없습니다.");
            return;
        }
        HidePanels();
        _supplyFollowSourceId = sourceId;
        _hoveredSupplyFollowTarget = null;
        _followSelectionFog = new MapSelectionFogOverlay();
        AddChild(_followSelectionFog);
        RefreshSupplyFollowSelectionFog();
        _log.Text = "따라갈 아군 전투부대를 맵에서 클릭하세요. 마우스를 올리면 선택 표시가 나옵니다. 우클릭 또는 Esc로 취소합니다.";
    }

    private void RefreshSupplyFollowSelectionFog()
    {
        if (_supplyFollowSourceId < 0 || _followSelectionFog is null) return;
        var openings = _hoveredSupplyFollowTarget is { } targetId
            && _armyTokens.TryGetValue(targetId.Value, out var token)
            && IsInstanceValid(token) && token.Visible
            ? new[] { _camera.UnprojectPosition(token.GlobalPosition + new Vector3(0f, .2f, 0f)) }
                .Select(point => new Vector3(point.X, point.Y, 38f)).ToArray()
            : [];
        _followSelectionFog.SetOpenings(openings);
    }

    private void HoverSupplyFollowTarget(CombatUnit? target)
    {
        var next = target?.Id;
        if (_hoveredSupplyFollowTarget == next) return;
        _hoveredSupplyFollowTarget = next;
        RefreshSupplyFollowSelectionFog();
    }

    private static bool IsSupplyFollowCandidate(CombatUnit unit)
        => unit.Pool.Active > 0 && !unit.IsSupply && !unit.IsTransport && !unit.IsWaitingDeployment;

    private CombatUnit? PickSupplyFollowTarget(Vector2 screen)
    {
        if (_supplyFollowSourceId < 0) return null;
        return DisplayedArmies.Where(unit => unit.Field.Owner == Player && IsSupplyFollowCandidate(unit)
                && _armyTokens.TryGetValue(unit.Id.Value, out var token)
                && IsInstanceValid(token) && token.Visible)
            .Select(unit => (Unit: unit, Distance: _camera.UnprojectPosition(
                _armyTokens[unit.Id.Value].GlobalPosition + new Vector3(0f, .2f, 0f)).DistanceTo(screen)))
            .Where(entry => entry.Distance <= 36f)
            .OrderBy(entry => entry.Distance).ThenBy(entry => entry.Unit.Id.Value)
            .Select(entry => entry.Unit).FirstOrDefault();
    }

    private void SelectSupplyFollowTarget(UnitId targetId)
    {
        var sourceId = new UnitId(_supplyFollowSourceId);
        var source = _state.Armies.FirstOrDefault(unit => unit.Id == sourceId);
        var target = _state.Armies.FirstOrDefault(unit => unit.Id == targetId);
        if (source is null || target is null) return;
        StopSupplyFollowTargeting();
        var targetName = _state.Generals.FirstOrDefault(general => general.Id == target.VanguardId)?.Name ?? "선택한 부대";
        ShowOfficerConfirm("따라가기 확인", $"보급부대가 {targetName} 부대를 따라가도록 명령하시겠습니까?",
            source.VanguardId ?? _state.Generals.First().Id, () =>
            {
                var result = SupplyFollowService.Assign(_state, sourceId, targetId);
                if (!result.Ok) { ShowNotice("따라가기 실패", result.Error ?? "대상을 선택할 수 없습니다."); return; }
                _state = result.State;
                Redraw("보급부대가 선택한 아군 전투부대를 따라갑니다.");
            }, $"{targetName} 부대를 따라가겠습니다.");
    }

    private void StopSupplyFollowTargeting()
    {
        _supplyFollowSourceId = -1;
        _hoveredSupplyFollowTarget = null;
        if (_followSelectionFog is not null && IsInstanceValid(_followSelectionFog)) _followSelectionFog.QueueFree();
        _followSelectionFog = null;
        if (_ring is not null) _ring.Visible = false;
    }

    private void RunSupplyFollowUiQa()
    {
        var city = _state.Cities.First(candidate => candidate.Owner == Player);
        CombatUnit MakeUnit(int id, int q, bool supply)
        {
            var field = new FieldUnit(new UnitId(id), Player,
                new SanguoSLG.Core.Spatial.HexCoord(q, city.Position.R), 2, 2, 1,
                SanguoSLG.Core.Spatial.MovementDomain.Land, UnitMode.Standby, null, id, 1);
            return new CombatUnit(field, new CombatStats(5000, 8, 8), new TroopPool(5000, 0),
                UnitCombatState.Create(60), MaxTroops: 10000, Provisions: 200,
                IsSupply: supply, TroopCode: "swordsman", VanguardId: _state.Generals.First().Id);
        }
        var source = MakeUnit(99011, city.Position.Q, true);
        var target = MakeUnit(99012, city.Position.Q + 1, false);
        _state = _state with { FieldArmies = _state.Armies.Concat([source, target]).ToList() };
        Redraw("따라가기 QA");
        BeginSupplyFollowTargeting(source.Id.Value);
        var started = _supplyFollowSourceId == source.Id.Value && _modalLayer is null
            && _followSelectionFog?.GetMeta("opening_count").AsInt32() == 0;
        var token = _armyTokens.GetValueOrDefault(target.Id.Value);
        var picked = token is not null && PickSupplyFollowTarget(_camera.UnprojectPosition(
            token.GlobalPosition + new Vector3(0f, .2f, 0f)))?.Id == target.Id;
        if (picked) HoverSupplyFollowTarget(target);
        var hovered = _followSelectionFog?.GetMeta("opening_count").AsInt32() == 1;
        if (picked) SelectSupplyFollowTarget(target.Id);
        var awaitingConfirm = _confirmLayer is not null
            && _state.Armies.First(unit => unit.Id == source.Id).Field.FollowUnitId is null;
        var accept = _confirmLayer?.FindChildren("*", "Button", true, false).OfType<Button>()
            .FirstOrDefault(button => button.Text == "확인");
        accept?.EmitSignal(Button.SignalName.Pressed);
        var assigned = _supplyFollowSourceId < 0
            && _state.Armies.First(unit => unit.Id == source.Id).Field.FollowUnitId == target.Id;
        var passed = started && picked && hovered && awaitingConfirm && assigned;
        GD.Print($"[supply-follow-ui-qa] passed={passed} started={started} picked={picked} hovered={hovered} confirm={awaitingConfirm} assigned={assigned}");
        GetTree().Quit(passed ? 0 : 1);
    }
}
