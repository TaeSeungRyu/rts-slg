using Godot;
using System;
using System.Linq;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

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
            Dbg($"FIELD_BUILDING attack u{exchange.Attacker.Value} -> b{building.Id.Value} "
                + $"({building.DefinitionCode}) damage={exchange.Damage} destroyed={exchange.Destroyed} at={attackTime:F2}s");
            _animAttacks.Add((attackTime, exchange.Attacker.Value, position));
            _animSiegeDmg.Add((attackTime + 0.35, position, exchange.Damage));
            if (turn.Units.Any(u => u.Id == exchange.Attacker && u.IsSupply))
                _animSupplyArrows.Add((attackTime + 0.08, exchange.Attacker.Value, position));
            if (exchange.Destroyed)
                // 피해 팝업 직후 제거한다. 그날 정산(settle)이나 7일 재생 종료를 기다리지 않는다.
                _animBuildingRemovals.Add((attackTime + 0.40, building.Id));
        }
    }

    private void ScheduleScoutPostRemovals(AdvanceTurn turn, double movementEndTime)
    {
        foreach (var id in turn.RemovedScoutPosts.Distinct())
        {
            var scout = _state.Buildings.FirstOrDefault(building => building.Id == id);
            if (scout is null) continue;
            var position = _view.HexToWorld(scout.Position) + new Vector3(0f, _view.TileTopY, 0f);
            _animDeathEffects.Add((movementEndTime, position));
            _animBuildingRemovals.Add((movementEndTime + 0.05, id));
            Dbg($"FIELD_SCOUT removed b{id.Value} at={movementEndTime:F2}s");
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
            // 다음 이동이 기병/상병/선박의 공격 복귀보다 먼저 시작되는 실제 재생 경합.
            foreach (var model in new[] { 1, 6, 7 })
            {
                var token = new UnitController3D();
                AddChild(token);
                token.InitDisplay(_view, Colors.Blue, model, new HexCoord(1, 5));
                token.DisplayStepTo(new HexCoord(1, 6), 0.10f);
                token.PlayAttackMotionToward(_view.HexToWorld(new HexCoord(1, 7)));
                await ToSignal(GetTree().CreateTimer(0.30), SceneTreeTimer.SignalName.Timeout);
                if (token.AttackMotionStartCount != 1) throw new Exception("도착 후 공격 누락");
                token.DisplayStepTo(new HexCoord(2, 6), 0.10f);
                await ToSignal(GetTree().CreateTimer(0.20), SceneTreeTimer.SignalName.Timeout);
                var expected = token.Position;
                var destination = _view.HexToWorld(new HexCoord(2, 6));
                if (new Vector2(expected.X - destination.X, expected.Z - destination.Z).Length() > 0.01f)
                    throw new Exception($"model={model}: 다음 이동 도착 실패");
                for (var frame = 0; frame < 150; frame++)
                {
                    await ToSignal(GetTree().CreateTimer(0.02), SceneTreeTimer.SignalName.Timeout);
                    if (token.Position.DistanceTo(expected) > 0.001f)
                        throw new Exception($"model={model}: 공격 복귀가 이동 좌표를 덮어씀");
                }
                token.QueueFree();
            }
            var sample = UnitAssembler.Assemble(new UnitId(99990), Player, new HexCoord(1, 7),
                UnitMode.Advance, null, 0, _state.Generals.First(g => g.Id.Value == 5),
                _state.Generals.First(g => g.Id.Value == 3), _troops.First(t => t.Code == "cavalry"),
                10000, _activeSkills.ToDictionary(s => s.Code), _passiveSkills.ToDictionary(s => s.Code),
                new CombatContext(MeleeEngagement: true, IncomingMelee: true, InField: true));
            foreach (var definition in _fieldBuildingDefinitions.Where(d => d.CanBeTargeted))
            {
                var damage = new BattleResolver(60).Damage(sample.Stats,
                    new CombatStats(1000, 0, definition.Defense, AptitudeGrade.C.Percent()));
                GD.Print($"FIELD_DAMAGE_QA {definition.Code}: stats={sample.Stats} raw={damage}");
            }
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
                || !_animBuildingRemovals.Any(x => x.Time == 2.40 && x.Building == building.Id))
                throw new Exception("건축물 공격/피해/파괴 재생 예약 누락");
            HideDestroyedFieldBuilding(building.Id);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_fieldBuildingLayer.GetNodeOrNull($"FieldBuilding_{building.Id.Value}") is not null
                || _fieldBuildingLayer.GetNodeOrNull($"FieldAllegiance_{building.Id.Value}") is not null)
                throw new Exception("파괴 후 건축물/소속 표시 잔존");
            GD.Print("FIELD_BUILDING_PLAYBACK_QA PASS: cavalry/elephant/ship movement overlaps attack recovery without position rollback; attack, damage, same-turn destruction");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
