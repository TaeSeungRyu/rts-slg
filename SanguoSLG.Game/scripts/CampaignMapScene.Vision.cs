using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using SanguoSLG.Core.Domain;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private readonly Dictionary<CityId, Label3D> _scoutLabels = new();
    private double _scoutFloatTime;

    private int ScoutDaysLeft(CityId city)
        => _state.Intel.Where(i => i.Faction == Player && i.City == city && i.ExpiresDay >= _state.Day)
            .Select(i => (int)Math.Min(int.MaxValue, (long)i.ExpiresDay - _state.Day + 1)).DefaultIfEmpty(0).Max();

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
