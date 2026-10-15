using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private readonly Dictionary<CityId, Label3D> _scoutLabels = new();
    private double _scoutFloatTime;
    private BattlefieldVision _vision = null!;
    private BattlefieldFogView _fog = null!;
    private IReadOnlySet<HexCoord> _visibleTiles = new HashSet<HexCoord>();
    private readonly Dictionary<int, HexCoord> _animationProductionPositions = new();
    private readonly HashSet<int> _lostProductionVision = new();
    private readonly List<(double Time, int Id)> _productionVisionLosses = new();
    private double _visionRefreshTime;
    private IReadOnlyList<CombatUnit> _displayArmies = Array.Empty<CombatUnit>();
    private PanelContainer? _intelPanel;
    private CityId? _intelPanelCity;

    private IReadOnlyList<CombatUnit> DisplayedArmies => (_advancing ? _displayArmies : _state.Armies)
        .Where(u => !u.IsWaitingDeployment && !IsFieldGarrison(u.Id)).ToList();

    private bool IsFieldGarrison(UnitId unitId)
    {
        var buildings = _advancing && _pendingState is not null ? _pendingState.Buildings : _state.Buildings;
        return buildings.Any(x => x.GarrisonUnit == unitId);
    }

    private bool CanInspectCity(City city)
        => BattlefieldVision.CanInspectCity(_state, Player, city, _visibleTiles);

    private bool IsDeclaredCity(City city)
        => _state.DeclaredWars.Any(w => w.ActiveOn(_state.Day) && w.Attacker == Player
            && w.City == city.Id && w.Defender == city.Owner);

    private string CityMapLabel(City city, int troops)
    {
        var factionName = _state.Factions.FirstOrDefault(f => f.Id == city.Owner)?.Name ?? "?";
        var declaration = IsDeclaredCity(city) ? $"⚔ 선전 포고 · {WarDaysLeft(city.Id)}일\n" : "";
        return declaration + $"{city.Name} [{factionName}]"
            + (CanInspectCity(city) ? $"\n성벽 {city.Wall}  병 {troops}" : "\n시야 밖");
    }

    private int WarDaysLeft(CityId city)
        => _state.DeclaredWars.Where(w => w.ActiveOn(_state.Day) && w.Attacker == Player && w.City == city)
            .Select(w => w.ExpiresDay - _state.Day).DefaultIfEmpty(0).Max();

    private void RunWarDeclarationQa()
    {
        var origin = _state.Cities.First(c => c.Owner == Player && !c.IsPort);
        var target = _state.Cities.First(c => c.Name == "한중");
        var officer = _state.GeneralsAt(origin.Id).First();
        var result = _commander.Issue(_state,
            new CommandRequest(origin.Id, CommandKind.DeclareWar, officer, TargetCity: target.Id));
        if (!result.Ok) { GD.PushError($"[war-declaration-qa] issue failed: {result.Error}"); GetTree().Quit(1); return; }
        _state = result.State;
        var pending = _state.Commands.Single(c => c.Kind == CommandKind.DeclareWar && c.TargetCity == target.Id);
        Redraw("war declaration pending QA");
        var hiddenBeforeArrival = !_cityLabels[target.Id.Value].Text.Contains("선전 포고", StringComparison.Ordinal);
        _state = new WorldEngine(_balance, _cb).AdvanceDays(_state, pending.CompletionDay - _state.Day);
        Redraw("war declaration QA");
        var marked = _cityLabels[target.Id.Value].Text.StartsWith("⚔ 선전 포고 · 100일\n", StringComparison.Ordinal);
        var remainsSecret = !CanInspectCity(target);
        _state = _state with { Day = _state.Day + CityWarDeclaration.DurationDays };
        Redraw("war declaration expiry QA");
        var expired = !_cityLabels[target.Id.Value].Text.Contains("선전 포고", StringComparison.Ordinal);
        var passed = hiddenBeforeArrival && marked && remainsSecret && expired;
        GD.Print($"[war-declaration-qa] pendingHidden={hiddenBeforeArrival} marked={marked} secret={remainsSecret} expired={expired} passed={passed}");
        GetTree().Quit(passed ? 0 : 1);
    }

    private bool CanSeeUnit(CombatUnit unit)
        => BattlefieldVision.CanSeeUnit(Player, unit, _visibleTiles);

    private bool IsVisibleAt(Vector3 position) => _visibleTiles.Contains(_view.WorldToHex(position));

    private CombatUnit PlaybackDeploymentState(CombatUnit unit) => unit with
    {
        DeploymentDelayDays = _animT >= unit.DeploymentDelayDays * DaySeconds ? 0 : unit.DeploymentDelayDays,
        AwaitingEgress = unit.AwaitingEgress && !_playbackReleasedDeployments.Contains(unit.Id.Value),
    };

    private void RefreshBattlefieldVision(bool playback = false)
    {
        var display = _state;
        if (playback)
        {
            foreach (var (id, token) in _productionTokens)
                _animationProductionPositions[id] = _view.WorldToHex(token.Position);
            foreach (var loss in _productionVisionLosses.Where(l => l.Time <= _animT))
                _lostProductionVision.Add(loss.Id);
            // 입성 연출에서 주둔군 토큰은 숨겨지거나 제거될 수 있다. 토큰 유무를
            // 시야 판정으로 사용하면 보루 안의 살아있는 부대 시야가 사라졌다가
            // 건물 파괴·진행 종료 후 갑자기 나타난다.
            var garrisonIds = _state.Buildings.Where(building => building.Owner == Player
                    && building.GarrisonUnit is not null)
                .Select(building => building.GarrisonUnit!.Value).ToHashSet();
            foreach (var (id, entry) in _animFieldGarrisonEntries)
                if (entry.Time <= _animT) garrisonIds.Add(new UnitId(id));
            display = display with
            {
                FieldArmies = display.Armies.Where(u => (_armyTokens.ContainsKey(u.Id.Value)
                        || garrisonIds.Contains(u.Id))
                    && !_animKills.Any(kill => kill.UnitId == u.Id.Value && kill.Time <= _animT
                        && (!_animFieldGarrisonEntries.TryGetValue(u.Id.Value, out var entry)
                            || kill.Time > entry.Time + 0.001)))
                    .Select(u => PlaybackDeploymentState(u) with
                    { Field = u.Field.MoveTo(garrisonIds.Contains(u.Id)
                        && _animFieldGarrisonEntries.TryGetValue(u.Id.Value, out var entry)
                            ? entry.Position
                            : _armyTokens.TryGetValue(u.Id.Value, out var token)
                                ? _view.WorldToHex(token.Position) : u.Field.Position) }).ToList(),
                ProductionOperations = display.ProductionOps.Where(o => !_lostProductionVision.Contains(o.Id))
                    .Select(o => o with { Position = _animationProductionPositions.GetValueOrDefault(o.Id, o.Position) }).ToList(),
            };
        }
        var visible = _vision.VisibleTiles(display, Player, _map);
        var changed = !_visibleTiles.SetEquals(visible);
        _visibleTiles = visible;
        _displayArmies = display.Armies;
        if (!playback || changed) _fog.Apply(_visibleTiles, _state.Cities.Where(IsDeclaredCity)
            .Select(city => city.Position).ToHashSet());
        RefreshFieldBuildingVisibility();
        foreach (var army in display.Armies)
        {
            if (_armyTokens.TryGetValue(army.Id.Value, out var token)) token.Visible = CanSeeUnit(army);
            if (_armyLabels.TryGetValue(army.Id.Value, out var label))
            {
                if (army.Field.Owner == WorldEngine.BanditFaction)
                {
                    var text = ArmyTroopLabel(army);
                    if (label.Text != text) label.Text = text;
                }
                label.Visible = !army.IsWaitingDeployment && (army.Field.Owner == Player
                    || (army.Field.Owner == WorldEngine.BanditFaction
                        || army.Field.Owner == EventHuntService.EventFaction) && CanSeeUnit(army));
            }
        }
        HideEnteredFieldGarrisons();
        foreach (var (id, token) in _productionTokens)
        {
            var op = display.ProductionOps.FirstOrDefault(o => o.Id == id);
            token.Visible = op is not null && (op.Owner == Player || _visibleTiles.Contains(op.Position));
        }
        foreach (var label in _facilityLayer.GetChildren().OfType<Label3D>())
            label.Visible = IsVisibleAt(label.Position);
        foreach (var city in _state.Cities)
        {
            var label = _cityLabels[city.Id.Value];
            var troops = CityTroopsForDisplay(city.Id);
            label.Text = CityMapLabel(city, troops);
            label.Modulate = new Color(city.Owner == Player ? Blue : Red,
                CanInspectCity(city) ? 1f : IsDeclaredCity(city) ? 0.8f : 0.5f);
        }
        if (_selectedUnitId >= 0 && !display.Armies.Any(u => u.Id.Value == _selectedUnitId && CanSeeUnit(u)))
            HidePanels();
        if (_terrainCard.Visible && _terrainHex is { } terrain && !_visibleTiles.Contains(terrain)) HidePanels();
        if (_advancing && _selected is { } cityId && _infoCard.Visible
            && _state.Cities.FirstOrDefault(c => c.Id == cityId) is { } selectedCity && !CanInspectCity(selectedCity))
            HidePanels();
        if (_intelPanel is not null && GodotObject.IsInstanceValid(_intelPanel) && !_intelPanel.IsQueuedForDeletion()
            && _modalLayer is not null && _modalLayer.IsAncestorOf(_intelPanel)
            && _intelPanelCity is { } intelCity && _state.Cities.FirstOrDefault(c => c.Id == intelCity) is { } cityInfo
            && !CanInspectCity(cityInfo))
        {
            CloseModal();
            _intelPanel = null;
            _intelPanelCity = null;
        }
    }

    private void RefreshPlaybackVision(double delta)
    {
        if (!_advancing) return;
        _visionRefreshTime += delta;
        if (_visionRefreshTime < 0.08) return;
        _visionRefreshTime = 0;
        RefreshBattlefieldVision(playback: true);
    }

    private void RefreshFieldBuildingVisibility()
    {
        foreach (var node in _fieldBuildingLayer.GetChildren().OfType<Node3D>())
            if (!node.IsQueuedForDeletion()) node.Visible = IsVisibleAt(node.Position);
    }

    private int ScoutDaysLeft(CityId city)
        => _state.Intel.Where(i => i.Faction == Player && i.City == city && i.ExpiresDay > _state.Day)
            .Select(i => i.ExpiresDay - _state.Day).DefaultIfEmpty(0).Max();

    private void RefreshScoutLabels()
    {
        var scouted = _state.Cities.Where(c => c.Owner != Player && _state.IsScouted(Player, c.Id))
            .ToDictionary(c => c.Id);
        foreach (var id in _scoutLabels.Keys.Where(id => !scouted.ContainsKey(id)).ToArray())
        {
            _scoutLabels[id].QueueFree();
            _scoutLabels.Remove(id);
        }
        foreach (var city in scouted.Values)
        {
            if (!_scoutLabels.TryGetValue(city.Id, out var label))
            {
                label = new Label3D
                {
                    Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                    Font = _font,
                    FontSize = 32,
                    OutlineSize = 8,
                    NoDepthTest = true,
                    Modulate = GoldBright,
                };
                AddChild(label);
                _scoutLabels.Add(city.Id, label);
            }
            label.Text = $"정찰완료 · {ScoutDaysLeft(city.Id)}일";
            label.Position = _view.HexToWorld(city.Position) + new Vector3(0, _view.TileTopY + 2.1f, 0);
        }
    }

    private void AnimateScoutLabels(double delta)
    {
        _scoutFloatTime += delta;
        foreach (var label in _scoutLabels.Values)
        {
            var position = label.Position;
            position.Y = _view.TileTopY + 2.1f + (float)Math.Sin(_scoutFloatTime * 2) * 0.08f;
            label.Position = position;
        }
    }
}
