using Godot;
using SanguoSLG.Core.Data;

namespace SanguoSLG.Game;

public partial class ScenarioFactionMap : Control
{
    private ScenarioDraft? _draft;
    private ScenarioMapPreset? _preset;
    private ImageTexture? _terrain;
    private readonly List<(Vector2I Tile, int FactionId, Color Color, string Name)> _cities = [];
    private int _selectedFactionId;
    public int CityCount => _cities.Count;
    public bool HasTerrain => _terrain is not null;

    public event Action<int>? FactionPicked;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        TextureFilter = TextureFilterEnum.Linear;
    }

    public override void _ExitTree()
    {
        _terrain?.Dispose();
        _terrain = null;
    }

    public void SetScenario(ScenarioDraft draft, ScenarioMapPreset preset, int selectedFactionId)
    {
        _draft = draft;
        _preset = preset;
        _selectedFactionId = selectedFactionId;
        _terrain?.Dispose();
        var sites = preset.Sites.ToDictionary(site => site.Id);
        var factions = draft.Factions.ToDictionary(faction => faction.Id);
        var territories = draft.Cities
            .Where(city => city.OwnerFactionId > 0 && sites.ContainsKey(city.SiteId)
                && factions.ContainsKey(city.OwnerFactionId))
            .Select(city => (X: sites[city.SiteId].Column, Y: sites[city.SiteId].Row,
                Color: new Color(factions[city.OwnerFactionId].Color))).ToArray();
        using var image = Image.CreateEmpty(preset.Width, preset.Height, false, Image.Format.Rgba8);
        for (var row = 0; row < preset.Height; row++)
            for (var column = 0; column < preset.Width; column++)
            {
                var terrain = preset.Rows[row][column];
                var color = TerrainColor(terrain);
                if (terrain is not ('O' or 'W' or 'R' or 'B' or 'V'))
                {
                    var closest = int.MaxValue;
                    var tint = Colors.Transparent;
                    foreach (var territory in territories)
                    {
                        var dx = column - territory.X;
                        var dy = row - territory.Y;
                        var distance = dx * dx + dy * dy;
                        if (distance >= closest) continue;
                        closest = distance;
                        tint = territory.Color;
                    }
                    if (closest < 625) color = color.Lerp(tint, 0.35f);
                }
                image.SetPixel(column, row, color);
            }
        _terrain = ImageTexture.CreateFromImage(image);
        _cities.Clear();
        foreach (var city in draft.Cities)
        {
            if (!sites.TryGetValue(city.SiteId, out var site) || site.Kind is not ("city" or "port")) continue;
            var color = factions.TryGetValue(city.OwnerFactionId, out var faction)
                ? new Color(faction.Color) : new Color("b7ad91");
            _cities.Add((new Vector2I(site.Column, site.Row), city.OwnerFactionId, color, site.Name));
        }
        QueueRedraw();
    }

    public void SelectFaction(int factionId)
    {
        _selectedFactionId = factionId;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var bounds = new Rect2(new Vector2(12, 12), Size - new Vector2(24, 24));
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("142522"));
        if (_terrain is null || _preset is null) return;
        DrawTextureRect(_terrain, bounds, false);
        DrawRect(bounds, new Color("9a8256"), false, 1.5f);
        foreach (var city in _cities)
        {
            var point = Project(city.Tile, bounds);
            var chosen = city.FactionId == _selectedFactionId;
            DrawCircle(point, chosen ? 6.4f : 4.2f, new Color("1a201d"));
            DrawCircle(point, chosen ? 4.7f : 2.8f, city.Color);
            if (chosen) DrawArc(point, 8.2f, 0, Mathf.Tau, 24, new Color("ffdf8e"), 1.5f, true);
        }
    }

    public override void _GuiInput(InputEvent input)
    {
        if (input is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click
            || _preset is null || _draft is null) return;
        var bounds = new Rect2(new Vector2(12, 12), Size - new Vector2(24, 24));
        var nearest = _cities.Where(city => city.FactionId > 0
                && _draft.Factions.Any(faction => faction.Id == city.FactionId && faction.Playable && !faction.Hidden))
            .Select(city => (City: city, Distance: Project(city.Tile, bounds).DistanceTo(click.Position)))
            .OrderBy(entry => entry.Distance).FirstOrDefault();
        if (nearest.Distance > 14f || nearest.City.FactionId <= 0) return;
        FactionPicked?.Invoke(nearest.City.FactionId);
        AcceptEvent();
    }

    private Vector2 Project(Vector2I tile, Rect2 bounds)
        => new(bounds.Position.X + (tile.X + 0.5f) / _preset!.Width * bounds.Size.X,
            bounds.Position.Y + (tile.Y + 0.5f) / _preset.Height * bounds.Size.Y);

    private static Color TerrainColor(char code) => code switch
    {
        'O' or 'W' or 'R' or 'B' or 'V' => new Color("467485"),
        'F' or 'f' or 'g' => new Color("426d56"),
        'M' or 'm' or 'n' or 'H' or 'h' or 'j' or 'I' or 'E' or 'L' => new Color("868a75"),
        'D' or 'Q' => new Color("b6a575"),
        'N' => new Color("668172"),
        _ => new Color("91a78a"),
    };
}
