using Godot;
using System;
using System.Linq;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private void ScheduleFieldBuildingCombat(AdvanceTurn turn, double attackTime, double settleTime)
    {
        foreach (var exchange in turn.FieldBuildingExchanges)
        {
            var building = _state.Buildings.FirstOrDefault(b => b.Id == exchange.Building);
            if (building is null) continue;
            var position = _view.HexToWorld(building.Position) + new Vector3(0, _view.TileTopY, 0);
            _animAttacks.Add((attackTime, exchange.Attacker.Value, position));
            _animSiegeDmg.Add((attackTime + 0.35, position, exchange.Damage));
            if (turn.Units.Any(u => u.Id == exchange.Attacker && u.IsSupply))
                _animSupplyArrows.Add((attackTime + 0.08, exchange.Attacker.Value, position));
            if (exchange.Destroyed)
                _animBuildingRemovals.Add((settleTime, building.Id));
        }
    }

    private void HideDestroyedFieldBuilding(FieldBuildingId id)
    {
        foreach (var prefix in new[] { "FieldBuilding", "FieldAllegiance", "FieldTools", "FieldConstructionDays", "ScoutDays" })
        {
            var node = _fieldBuildingLayer.GetNodeOrNull<Node3D>($"{prefix}_{id.Value}");
            if (node is null) continue;
            node.Visible = false;
            node.QueueFree();
        }
        _scoutDayLabels.Remove(id);
        if (_selectedFieldBuildingId == id.Value) ClearFieldBuildingRange();
    }

    private async void RunFieldBuildingPlaybackQa()
    {
        try
        {
            var building = _state.Buildings.First(b => b.Owner != Player && b.DefinitionCode != "scout_post");
            var attacker = new UnitId(99991);
            var turn = new AdvanceTurn([], new AdvanceResult([], [], StopReason.AllArrived, 1), null,
                new System.Collections.Generic.Dictionary<UnitId, ActiveSkill>(),
                new System.Collections.Generic.Dictionary<UnitId, Stratagem>(),
                new System.Collections.Generic.Dictionary<UnitId, int>(),
                new System.Collections.Generic.Dictionary<UnitId, int>(),
                FieldBuildingCombatExchanges: [new(building.Id, attacker, 500, true)]);
            ScheduleFieldBuildingCombat(turn, 2, 2.55);
            if (!_animAttacks.Any(x => x.Time == 2 && x.UnitId == attacker.Value)
                || !_animSiegeDmg.Any(x => x.Time == 2.35 && x.Damage == 500)
                || !_animBuildingRemovals.Any(x => x.Time == 2.55 && x.Building == building.Id))
                throw new Exception("건축물 공격/피해/파괴 재생 예약 누락");
            HideDestroyedFieldBuilding(building.Id);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_fieldBuildingLayer.GetNodeOrNull($"FieldBuilding_{building.Id.Value}") is not null
                || _fieldBuildingLayer.GetNodeOrNull($"FieldAllegiance_{building.Id.Value}") is not null)
                throw new Exception("파괴 후 건축물/소속 표시 잔존");
            GD.Print("FIELD_BUILDING_PLAYBACK_QA PASS: attack, damage, same-turn destruction");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
