using Godot;
using System;
using System.Linq;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private readonly System.Collections.Generic.HashSet<FieldBuildingId> _playbackDestroyedBuildings = [];

    private void ShowEnteredFieldGarrisons()
    {
        var visibleIds = _state.Buildings.Where(building => building.GarrisonUnit is not null
                && !_playbackDestroyedBuildings.Contains(building.Id))
            .Select(building => building.GarrisonUnit!.Value.Value)
            .Concat(_animFieldGarrisonEntries.Where(entry => entry.Value.Time <= _animT
                && !_state.Buildings.Any(building => building.Position == entry.Value.Position
                    && _playbackDestroyedBuildings.Contains(building.Id)))
                .Select(entry => entry.Key))
            .Distinct();
        foreach (var id in visibleIds)
        {
            if (!_armyTokens.TryGetValue(id, out var token) || !GodotObject.IsInstanceValid(token)
                || token.IsQueuedForDeletion()) continue;
            token.Visible = true;
            token.Position = new Vector3(token.Position.X, _view.TileTopY + 0.55f, token.Position.Z);
            if (_armyLabels.TryGetValue(id, out var label))
                label.Visible = (_state.Armies.FirstOrDefault(unit => unit.Id.Value == id)
                    ?? _pendingState.Armies.FirstOrDefault(unit => unit.Id.Value == id))?.Field.Owner == Player;
        }
    }

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
            if (turn.FiredActives.TryGetValue(exchange.Attacker, out var active))
                _animSiegeSkillEffects.Add((attackTime + .14, exchange.Attacker.Value,
                    position + new Vector3(0f, .25f, 0f), active));
            _animSiegeDmg.Add((attackTime + 0.35, position, exchange.Damage));
            if (turn.Units.Any(u => u.Id == exchange.Attacker && u.IsSupply))
                _animSupplyArrows.Add((attackTime + 0.08, exchange.Attacker.Value, position));
            if (exchange.DamageToAttacker > 0)
            {
                // 주둔군은 토큰이 숨겨져 있으므로 건물 위치에서 반격 피드백을 재생한다.
                _animArrows.Add((attackTime + 0.42, position + new Vector3(0f, 0.85f, 0f),
                    exchange.Attacker.Value));
                _animDmg.Add((attackTime + 0.52, exchange.Attacker.Value, exchange.DamageToAttacker));
            }
            if (exchange.Destroyed)
            {
                // 피해 팝업 직후 제거한다. 그날 정산(settle)이나 7일 재생 종료를 기다리지 않는다.
                _animBuildingRemovals.Add((attackTime + 0.40, building.Id));
                if (exchange.ExposedGarrison is { } garrisonId)
                {
                    if (exchange.DamageToGarrison > 0)
                        _animDmg.Add((attackTime + 0.48, garrisonId.Value, exchange.DamageToGarrison));
                }
            }
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

    private void PlayFormationBubbles(Vector3 position)
    {
        var anchor = new Node3D { Name = "FormationBubbles" };
        AddChild(anchor);
        anchor.GlobalPosition = position + new Vector3(0f, 0.10f, 0f);
        EffectView.Attach(anchor, EffectKind.Bubbles, 1.05f);
        GetTree().CreateTimer(2.5).Timeout += () =>
        {
            if (GodotObject.IsInstanceValid(anchor) && !anchor.IsQueuedForDeletion()) anchor.QueueFree();
        };
    }

    private void HideDestroyedFieldBuilding(FieldBuildingId id)
    {
        // The simulation snapshot is committed only at the end of playback. Keep a
        // live tombstone so clicks and redraws cannot resurrect an already destroyed fort.
        _playbackDestroyedBuildings.Add(id);
        // 진행 시작 Redraw에서 QueueFree 대기 중인 이전 노드와 이름이 충돌하면 Godot이
        // 새 노드 이름을 자동 변경한다. 이름 한 개만 찾지 말고 건축물 id 메타로 전부 제거한다.
        foreach (var node in _fieldBuildingLayer.GetChildren().OfType<Node3D>().Where(node =>
                     node.HasMeta("field_building_id") && node.GetMeta("field_building_id").AsInt32() == id.Value).ToList())
        {
            node.Visible = false;
            node.QueueFree();
        }
        // 구버전/테스트 노드는 메타가 없을 수 있어 정확한 기존 이름도 정리한다.
        foreach (var prefix in new[] { "FieldBuilding", "FieldAllegiance", "FieldTools", "FieldConstructionDays", "ScoutDays" })
        {
            var legacy = _fieldBuildingLayer.GetNodeOrNull<Node3D>($"{prefix}_{id.Value}");
            if (legacy is null || legacy.IsQueuedForDeletion()) continue;
            legacy.Visible = false;
            legacy.QueueFree();
        }
        _scoutDayLabels.Remove(id);
        if (_selectedFieldBuildingId == id.Value) ClearFieldBuildingRange();
        // 보루·진법 안에 있던 부대는 건축물 파괴와 동시에 지도에 다시 나타난다.
        var garrison = _state.Buildings.FirstOrDefault(building => building.Id == id)?.GarrisonUnit;
        if (garrison is { } unitId)
        {
            if (!_armyTokens.TryGetValue(unitId.Value, out var token)
                || !GodotObject.IsInstanceValid(token) || token.IsQueuedForDeletion())
            {
                var unit = _state.Armies.FirstOrDefault(army => army.Id == unitId)
                    ?? _pendingState.Armies.FirstOrDefault(army => army.Id == unitId);
                if (unit is null || unit.Pool.Active <= 0) return;
                token = new UnitController3D();
                AddChild(token);
                token.InitDisplay(_view, ArmyFactionColor(unit.Field.Owner),
                    unit.IsArmyGroup ? UnitController3D.ArmyGroupModelIndex
                        : unit.IsSupply ? UnitController3D.SupplyModelIndex
                        : TroopModelIndex.GetValueOrDefault(unit.TroopCode, 0), unit.Field.Position);
                token.SetFormationSize(unit.IsSupply || unit.IsArmyGroup ? 1 : FormationFor(unit.Pool.Active));
                _armyTokens[unitId.Value] = token;
            }
            token.Visible = true;
            if (_armyLabels.TryGetValue(unitId.Value, out var label))
                label.Visible = _state.Armies.FirstOrDefault(unit => unit.Id == unitId)?.Field.Owner == Player;
        }
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
                var target = new FieldBuilding(new FieldBuildingId(99000 + definition.Kind.GetHashCode()),
                    definition.Code, new FactionId(2), new HexCoord(1, 8), definition.MaxHitPoints, 0, 0);
                var qaState = _state with { FieldArmies = [sample], FieldBuildings = [target] };
                var exchange = AssertSingleFieldBuildingExchange(
                    new FieldBuildingCombat(new BattleResolver(60), _fieldBuildingDefinitions, _troops)
                        .Resolve(qaState, [sample]), definition.Code);
                if (exchange.Damage != 537 || exchange.Destroyed)
                    throw new Exception($"{definition.Code}: 여포/관우 기병 건물 피해 불일치 damage={exchange.Damage} destroyed={exchange.Destroyed}");
                GD.Print($"FIELD_DAMAGE_QA {definition.Code}: stats={sample.Stats} buildingDamage={exchange.Damage} hp={definition.MaxHitPoints}");
            }
            var building = _state.Buildings.First(b => b.Owner != Player && b.DefinitionCode == "fort");
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
            // QueueFree 대기 이름 충돌을 재현: 이름이 달라도 같은 id 메타를 가진 현재 모델은 제거돼야 한다.
            var renamed = new Node3D { Name = $"FieldBuilding_{building.Id.Value}_renamed" };
            renamed.SetMeta("field_building_id", building.Id.Value);
            _fieldBuildingLayer.AddChild(renamed);
            HideDestroyedFieldBuilding(building.Id);
            if (CanPlayerSeeFieldBuilding(building)
                || _state.Buildings.FirstOrDefault(b => b.Position == building.Position && CanPlayerSeeFieldBuilding(b))?.Id == building.Id)
                throw new Exception("파괴 직후 보루 클릭 대상이 남아 있음");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_fieldBuildingLayer.GetChildren().OfType<Node3D>().Any(node =>
                    node.HasMeta("field_building_id") && node.GetMeta("field_building_id").AsInt32() == building.Id.Value)
                || _fieldBuildingLayer.GetNodeOrNull($"FieldBuilding_{building.Id.Value}") is not null
                || _fieldBuildingLayer.GetNodeOrNull($"FieldAllegiance_{building.Id.Value}") is not null)
                throw new Exception("파괴 후 건축물/소속 표시 잔존");
            var beforeState = _state;
            var exposed = sample with { Field = sample.Field with { Position = building.Position } };
            var occupied = building with { Id = new FieldBuildingId(99992), Owner = Player,
                GarrisonUnit = exposed.Id };
            _state = _state with { FieldArmies = _state.Armies.Append(exposed).ToList(),
                FieldBuildings = _state.Buildings.Append(occupied).ToList() };
            Redraw("fort garrison visibility QA");
            if (!_armyTokens.TryGetValue(exposed.Id.Value, out var enteredToken) || !enteredToken.Visible)
                throw new Exception("보루 첫 입성 직후 주둔 부대가 보이지 않음");
            _animFieldGarrisonEntries[exposed.Id.Value] = (0, occupied.Position);
            enteredToken.Visible = false;
            ShowEnteredFieldGarrisons();
            if (!enteredToken.Visible || enteredToken.Position.Y < _view.TileTopY + 0.5f)
                throw new Exception("보루 첫 입성 재생에서 부대가 건물에 가려짐");
            _state = _state with { FieldBuildings = _state.Buildings.Select(item => item.Id == occupied.Id
                ? item with { DefinitionCode = "formation" } : item).ToList() };
            Redraw("formation garrison visibility QA");
            if (!_armyTokens.TryGetValue(exposed.Id.Value, out var formationToken) || !formationToken.Visible
                || formationToken.Position.Y < _view.TileTopY + 0.5f)
                throw new Exception("진법 첫 입성 직후 주둔 부대가 보이지 않음");
            _animFieldGarrisonEntries.Remove(exposed.Id.Value);
            HideDestroyedFieldBuilding(occupied.Id);
            if (!_armyTokens.TryGetValue(exposed.Id.Value, out var revealed) || !revealed.Visible)
                throw new Exception("보루 파괴 직후 주둔 부대가 보이지 않음");
            revealed.QueueFree();
            _armyTokens.Remove(exposed.Id.Value);
            _state = beforeState;
            GD.Print("FIELD_BUILDING_PLAYBACK_QA PASS: cavalry/elephant/ship movement overlaps attack recovery without position rollback; attack, damage, same-turn destruction");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private static FieldBuildingExchange AssertSingleFieldBuildingExchange(FieldBuildingCombatResult result, string code)
        => result.Exchanges.Count == 1
            ? result.Exchanges[0]
            : throw new Exception($"{code}: 건축물 교환 수 불일치 count={result.Exchanges.Count}");
}
