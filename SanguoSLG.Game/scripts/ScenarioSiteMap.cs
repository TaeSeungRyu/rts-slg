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
        CustomMinimumSize = new Vector2(660, 360);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Stop;
    }

    private Vector2 Point(MapEditorSite site)
        => new((site.Column + 0.5f) / _map.Width * Size.X,
            (site.Row + 0.5f) / _map.Height * Size.Y);

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("172d30"));
        var stepX = Math.Max(1, _map.Width / 90);
        var stepY = Math.Max(1, _map.Height / 60);
        for (var row = 0; row < _map.Height; row += stepY)
            for (var column = 0; column < _map.Width; column += stepX)
            {
                var color = _map.Rows[row][column] switch
                {
                    'W' or 'O' or 'R' => new Color("447c90"),
                    'M' or 'H' or 'I' => new Color("6d766c"),
                    'F' => new Color("3d7657"),
                    'D' or 'Q' => new Color("b9a575"),
                    _ => new Color("80a182"),
                };
                DrawRect(new Rect2(new Vector2(column * Size.X / _map.Width,
                    row * Size.Y / _map.Height),
                    new Vector2(stepX * Size.X / _map.Width + 1,
                        stepY * Size.Y / _map.Height + 1)), color);
            }
        foreach (var site in _map.Sites.Where(site => _cities.ContainsKey(site.Id)))
        {
            var city = _cities[site.Id];
            var color = _factions.GetValueOrDefault(city.OwnerFactionId, new Color("eeeecc"));
            var point = Point(site);
            DrawCircle(point, site.Id == _selectedId ? 8f : 4.5f, new Color("182523"));
            DrawCircle(point, site.Id == _selectedId ? 6f : 3.5f, color);
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
