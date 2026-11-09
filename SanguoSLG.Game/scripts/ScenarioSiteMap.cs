using Godot;
using SanguoSLG.Core.Data;

namespace SanguoSLG.Game;

public sealed partial class ScenarioSiteMap : Control
{
    private readonly ScenarioMapPreset _map;
    private readonly IReadOnlyDictionary<int, ScenarioCityEntry> _cities;
    private readonly IReadOnlyDictionary<int, Color> _factions;
    private readonly Action<int> _selected;
    private readonly int _selectedId;

    public ScenarioSiteMap(ScenarioMapPreset map, IReadOnlyList<ScenarioCityEntry> cities,
        IReadOnlyList<ScenarioFactionEntry> factions, int selectedId, Action<int> selected)
    {
        _map = map;
        _cities = cities.ToDictionary(city => city.SiteId);
        _factions = factions.ToDictionary(faction => faction.Id,
            faction => new Color(faction.Color));
        _selectedId = selectedId;
        _selected = selected;
        CustomMinimumSize = new Vector2(540, 230);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Stop;
    }

    private Rect2 MapRect()
    {
        var mapRatio = _map.Width / (float)_map.Height;
        var viewRatio = Size.X / Math.Max(1f, Size.Y);
        var mapSize = viewRatio > mapRatio
            ? new Vector2(Size.Y * mapRatio, Size.Y)
            : new Vector2(Size.X, Size.X / mapRatio);
        return new Rect2((Size - mapSize) / 2f, mapSize);
    }

    private Vector2 Point(MapEditorSite site)
    {
        var rect = MapRect();
        return rect.Position + new Vector2((site.Column + 0.5f) / _map.Width * rect.Size.X,
            (site.Row + 0.5f) / _map.Height * rect.Size.Y);
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("100c0a"));
        var rect = MapRect();
        DrawRect(rect, new Color("172d30"));
        var stepX = Math.Max(1, _map.Width / 90);
        var stepY = Math.Max(1, _map.Height / 60);
        for (var row = 0; row < _map.Height; row += stepY)
            for (var column = 0; column < _map.Width; column += stepX)
            {
                var color = _map.Rows[row][column] switch
                {
                    'W' or 'O' or 'R' => new Color("447c90"),
                    'M' or 'm' or 'n' or 'H' or 'h' or 'j' or 'I' => new Color("6d766c"),
                    'F' or 'f' or 'g' => new Color("3d7657"),
                    'D' or 'Q' => new Color("b9a575"),
                    _ => new Color("80a182"),
                };
                DrawRect(new Rect2(rect.Position + new Vector2(column * rect.Size.X / _map.Width,
                    row * rect.Size.Y / _map.Height),
                    new Vector2(stepX * rect.Size.X / _map.Width + 1,
                        stepY * rect.Size.Y / _map.Height + 1)), color);
            }
        foreach (var site in _map.Sites.Where(site => _cities.ContainsKey(site.Id)))
        {
            var city = _cities[site.Id];
            var color = _factions.GetValueOrDefault(city.OwnerFactionId, new Color("eeeecc"));
            var point = Point(site);
            if (site.Id == _selectedId)
            {
                DrawCircle(point, 10f, new Color("d6ac62"));
                DrawCircle(point, 8f, new Color("182523"));
                DrawCircle(point, 5.5f, color);
            }
            else
            {
                DrawCircle(point, 4.5f, new Color("182523"));
                DrawCircle(point, 3.5f, color);
            }
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion)
        {
            var nearest = _map.Sites.Where(site => _cities.ContainsKey(site.Id))
                .OrderBy(site => Point(site).DistanceTo(motion.Position)).FirstOrDefault();
            TooltipText = nearest is not null && Point(nearest).DistanceTo(motion.Position) < 14f
                ? nearest.Name : "지도의 점을 눌러 거점을 선택하세요";
        }
        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click) return;
        var target = _map.Sites.Where(site => _cities.ContainsKey(site.Id))
            .OrderBy(site => Point(site).DistanceTo(click.Position)).FirstOrDefault();
        if (target is null || Point(target).DistanceTo(click.Position) > 16f) return;
        _selected(target.Id);
        AcceptEvent();
    }
}
