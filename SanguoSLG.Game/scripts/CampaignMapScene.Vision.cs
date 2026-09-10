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

    private IReadOnlyList<CombatUnit> DisplayedArmies => _advancing ? _displayArmies : _state.Armies;

    private bool CanInspectCity(City city)
        => BattlefieldVision.CanInspectCity(_state, Player, city, _visibleTiles);

    private bool CanSeeUnit(CombatUnit unit)
        => BattlefieldVision.CanSeeUnit(Player, unit, _visibleTiles);

    private bool IsVisibleAt(Vector3 position) => _visibleTiles.Contains(_view.WorldToHex(position));

    private void RefreshBattlefieldVision(bool playback = false)
    {
        var display = _state;
        if (playback)
        {
            foreach (var (id, token) in _productionTokens)
                _animationProductionPositions[id] = _view.WorldToHex(token.Position);
            foreach (var loss in _productionVisionLosses.Where(l => l.Time <= _animT))
                _lostProductionVision.Add(loss.Id);
            display = display with
            {
                FieldArmies = display.Armies.Where(u => _armyTokens.ContainsKey(u.Id.Value))
                    .Select(u => u with { Field = u.Field.MoveTo(_view.WorldToHex(_armyTokens[u.Id.Value].Position)) }).ToList(),
                ProductionOperations = display.ProductionOps.Where(o => !_lostProductionVision.Contains(o.Id))
                    .Select(o => o with { Position = _animationProductionPositions.GetValueOrDefault(o.Id, o.Position) }).ToList(),
            };
        }
        var visible = _vision.VisibleTiles(display, Player, _map);
        var changed = !_visibleTiles.SetEquals(visible);
        _visibleTiles = visible;
        _displayArmies = display.Armies;
        if (!playback || changed) _fog.Apply(_visibleTiles);
        foreach (var army in display.Armies)
        {
            if (_armyTokens.TryGetValue(army.Id.Value, out var token)) token.Visible = CanSeeUnit(army);
            if (_armyLabels.TryGetValue(army.Id.Value, out var label)) label.Visible = army.Field.Owner == Player;
        }
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
            var factionName = _state.Factions.FirstOrDefault(f => f.Id == city.Owner)?.Name ?? "?";
            var troops = _state.Garrisons.Where(g => g.City == city.Id).Sum(g => g.Troops);
            label.Text = $"{city.Name} [{factionName}]" + (CanInspectCity(city) ? $"\n성벽 {city.Wall}  병 {troops}" : "\n시야 밖");
            label.Modulate = new Color(city.Owner == Player ? Blue : Red, CanInspectCity(city) ? 1f : 0.5f);
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
