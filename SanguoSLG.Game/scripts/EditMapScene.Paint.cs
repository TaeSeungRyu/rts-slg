using Godot;
using SanguoSLG.Core.Data;

namespace SanguoSLG.Game;

public sealed partial class EditMapScene
{
    private bool _paintMode;
    private bool _painting;
    private bool _paintChanged;
    private char _paintCode = 'P';
    private ScenarioMapPreset? _paintBefore;
    private Vector2 _lastPaintScreen;
    private readonly HashSet<Vector2I> _paintDirty = [];

    private void AddPaintControls(HBoxContainer bar)
    {
        var toggle = new Button
        {
            Name = "TerrainPaintToggle", Text = "지형 그리기", ToggleMode = true,
            CustomMinimumSize = new Vector2(105, 26),
            TooltipText = "선택한 지형을 왼쪽 마우스 드래그로 칠합니다. 가운데 버튼은 지도 이동입니다.",
        };
        toggle.Toggled += enabled =>
        {
            if (_painting) EndPaintStroke();
            _paintMode = enabled;
            if (_editorPalette is not null)
            {
                _editorPalette.QueueFree();
                _editorPalette = null;
            }
            _selectionVisual.Visible = false;
            _tileInfo.Text = enabled
                ? $"지형 그리기: {Terrain[_paintCode].Name} · 왼쪽 드래그로 변경, 가운데 드래그로 지도 이동"
                : "지형 그리기 종료 · 타일을 클릭해 편집 명령을 열 수 있습니다.";
        };
        bar.AddChild(toggle);
        var choices = Terrain.Keys.ToArray();
        var picker = new OptionButton { Name = "TerrainPaintKind", CustomMinimumSize = new Vector2(118, 26),
            TooltipText = "그릴 지형을 선택하세요." };
        foreach (var code in choices) picker.AddItem(Terrain[code].Name);
        picker.Selected = 0;
        picker.ItemSelected += index =>
        {
            _paintCode = choices[(int)index];
            if (_paintMode) _tileInfo.Text = $"지형 그리기: {Terrain[_paintCode].Name}";
        };
        bar.AddChild(picker);
    }

    private Vector2I? PaintTileAtScreen(Vector2 screen)
    {
        if (_mapViewport is not null)
        {
            var origin = _mapCamera.ProjectRayOrigin(screen);
            var ray = _mapCamera.ProjectRayNormal(screen);
            if (Mathf.Abs(ray.Y) < 0.001f) return null;
            var world = origin + ray * ((_mapView.TileTopY - origin.Y) / ray.Y);
            var tile = Offset(_mapView.WorldToHex(world));
            return Inside(tile) ? tile : null;
        }
        var point = (screen - _pan) / _scale;
        var column = Mathf.RoundToInt(point.X / StepX);
        if (column < 0 || column >= _width) return null;
        var row = Mathf.RoundToInt((point.Y - (column % 2) * StepY * 0.5f) / StepY);
        var result = new Vector2I(column, row);
        return Inside(result) && point.DistanceTo(Center(column, row)) <= Radius * 1.05f
            ? result : null;
    }

    private void BeginPaintStroke(Vector2 screen)
    {
        if (!_paintMode || _editorModal is not null) return;
        _painting = true;
        _paintChanged = false;
        _paintBefore = Draft();
        _lastPaintScreen = screen;
        PaintSegment(screen);
    }

    private void PaintSegment(Vector2 screen)
    {
        if (!_painting) return;
        var previous = _lastPaintScreen;
        var steps = Mathf.Max(1, Mathf.CeilToInt(previous.DistanceTo(screen) / 5f));
        for (var index = 0; index <= steps; index++)
        {
            var tile = PaintTileAtScreen(previous.Lerp(screen, (float)index / steps));
            if (tile is not { } target) continue;
            if (_mapViewport is not null) ShowSelectedTile(target);
            PaintTile(target);
        }
        _lastPaintScreen = screen;
        RefreshPaintVisuals();
    }

    private bool PaintTile(Vector2I tile)
    {
        if (!Inside(tile) || _rows[tile.Y][tile.X] == _paintCode
            || !CanChangeTerrain(tile, _paintCode)) return false;
        var chars = _rows[tile.Y].ToCharArray();
        chars[tile.X] = _paintCode;
        _rows[tile.Y] = new string(chars);
        _paintDirty.Add(tile);
        _paintChanged = true;
        return true;
    }

    private void RefreshPaintVisuals()
    {
        if (_paintDirty.Count == 0) return;
        foreach (var tile in _paintDirty.SelectMany(tile => Axial(tile).Neighbors()
                     .Select(Offset).Append(tile)).Where(Inside).Distinct())
            if (_visibleTiles.Remove(tile, out var visual)) visual.QueueFree();
        _paintDirty.Clear();
        if (_mapViewport is not null) RefreshVisibleTerrain();
        QueueRedraw();
    }

    private void EndPaintStroke()
    {
        if (!_painting) return;
        _painting = false;
        RefreshPaintVisuals();
        if (_paintChanged && _paintBefore is not null)
            RecordChange(_paintBefore);
        _paintBefore = null;
        _paintChanged = false;
    }
}
