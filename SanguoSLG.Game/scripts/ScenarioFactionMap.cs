using Godot;
using SanguoSLG.Core.Data;

namespace SanguoSLG.Game;

public partial class ScenarioFactionMap : Control
{
    private readonly record struct MapSite(Vector2I Tile, int FactionId, Color Color,
        string Name, string FactionName, bool Selectable);

    private ScenarioDraft? _draft;
    private ScenarioMapPreset? _preset;
    private ImageTexture? _terrain;
    private readonly List<MapSite> _cities = [];
    private PanelContainer? _tooltipPanel;
    private Label? _tooltipLabel;
    private int _selectedFactionId;
    private int _selectedSiteIndex = -1;
    private int _hoveredSite = -1;
    public int CityCount => _cities.Count;
    public bool HasTerrain => _terrain is not null;
    public int SelectedFactionId => _selectedFactionId;
    public bool HasSelectedSite => _selectedSiteIndex >= 0;
    public int SelectableFactionCount => _cities.Where(city => city.Selectable)
        .Select(city => city.FactionId).Distinct().Count();
    public int FirstSelectableFactionId => _cities.FirstOrDefault(city => city.Selectable).FactionId;
    public Vector2 FirstSelectableSitePosition
    {
        get
        {
            var city = _cities.FirstOrDefault(entry => entry.Selectable);
            return city.FactionId > 0 ? Project(city.Tile, CalculateMapBounds()) : new Vector2(-1, -1);
        }
    }

    public event Action<int>? FactionPicked;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        TextureFilter = TextureFilterEnum.Linear;
        BuildMapTooltip();
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
            if (!sites.TryGetValue(city.SiteId, out var site)
                || site.Kind is not ("city" or "port" or "gate")) continue;
            var knownFaction = factions.TryGetValue(city.OwnerFactionId, out var faction);
            var color = knownFaction ? new Color(faction!.Color) : new Color("b7ad91");
            var selectable = knownFaction && faction!.Playable && !faction.Hidden;
            _cities.Add(new MapSite(new Vector2I(site.Column, site.Row), city.OwnerFactionId,
                color, site.Name, knownFaction ? faction!.Name : "중립", selectable));
        }
        _hoveredSite = -1;
        _selectedSiteIndex = -1;
        if (_tooltipPanel is not null) _tooltipPanel.Visible = false;
        TooltipText = "거점을 눌러 플레이할 세력을 선택하십시오.";
        QueueRedraw();
    }

    public void ClearScenario()
    {
        _draft = null;
        _preset = null;
        _terrain?.Dispose();
        _terrain = null;
        _cities.Clear();
        _selectedFactionId = 0;
        _selectedSiteIndex = -1;
        _hoveredSite = -1;
        TooltipText = "";
        QueueRedraw();
    }

    public void SelectFaction(int factionId)
    {
        _selectedFactionId = factionId;
        if (factionId <= 0) _selectedSiteIndex = -1;
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("121a18"));
        if (_terrain is null || _preset is null) return;
        var bounds = CalculateMapBounds();
        DrawTextureRect(_terrain, bounds, false);
        DrawRect(bounds, new Color("c6a258"), false, 1.5f);
        for (var index = 0; index < _cities.Count; index++)
        {
            var city = _cities[index];
            var point = Project(city.Tile, bounds);
            var chosen = city.FactionId == _selectedFactionId;
            var hovered = index == _hoveredSite;
            DrawCircle(point, chosen ? 7.2f : 4.5f, new Color("15100d"));
            DrawCircle(point, chosen ? 5.2f : 3.0f, city.Color);
            if (chosen)
            {
                DrawArc(point, 8.8f, 0, Mathf.Tau, 28, new Color("ffe29a"), 2.0f, true);
                DrawArc(point, 11.2f, 0, Mathf.Tau, 28, new Color(0.95f, 0.67f, 0.22f, 0.5f), 1.0f, true);
            }
            if (index == _selectedSiteIndex)
            {
                DrawCircle(point, 2.1f, new Color("fff1b3"));
                DrawCircle(point, 0.9f, Colors.White);
            }
            if (hovered) DrawArc(point, chosen ? 10.4f : 7.2f, 0, Mathf.Tau, 24,
                new Color("fff0b4"), 1.4f, true);
        }
    }

    public override void _GuiInput(InputEvent input)
    {
        if (_preset is null || _draft is null) return;
        var bounds = CalculateMapBounds();
        if (input is InputEventMouseMotion motion)
        {
            _hoveredSite = NearestSite(motion.Position, bounds, false);
            if (_hoveredSite >= 0)
            {
                var city = _cities[_hoveredSite];
                var tooltip = city.Selectable
                    ? $"{city.Name}\n{city.FactionName} 세력 · 클릭하여 선택"
                    : $"{city.Name}\n{city.FactionName} · 선택할 수 없는 세력";
                TooltipText = tooltip;
                ShowMapTooltip(tooltip, motion.Position);
                MouseDefaultCursorShape = city.Selectable ? CursorShape.PointingHand : CursorShape.Arrow;
            }
            else
            {
                TooltipText = "거점을 눌러 플레이할 세력을 선택하십시오.";
                if (_tooltipPanel is not null) _tooltipPanel.Visible = false;
                MouseDefaultCursorShape = CursorShape.Arrow;
            }
            QueueRedraw();
            return;
        }
        if (input is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click) return;
        if (SelectFactionAt(click.Position)) AcceptEvent();
    }

    private void BuildMapTooltip()
    {
        _tooltipPanel = new PanelContainer
        {
            Name = "ScenarioMapTooltip",
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 20
        };
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.075f, 0.03f, 0.027f, 0.97f),
            BorderColor = new Color("d7ad59")
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(4);
        style.SetContentMarginAll(7);
        _tooltipPanel.AddThemeStyleboxOverride("panel", style);
        AddChild(_tooltipPanel);
        _tooltipLabel = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _tooltipLabel.AddThemeFontOverride("font", GD.Load<FontFile>("res://assets/fonts/Pretendard-SemiBold.otf"));
        _tooltipLabel.AddThemeFontSizeOverride("font_size", 12);
        _tooltipLabel.AddThemeColorOverride("font_color", new Color("eee3cc"));
        _tooltipPanel.AddChild(_tooltipLabel);
    }

    private void ShowMapTooltip(string text, Vector2 pointer)
    {
        if (_tooltipPanel is null || _tooltipLabel is null) return;
        _tooltipLabel.Text = text;
        _tooltipPanel.Visible = true;
        var proposed = pointer + new Vector2(14, 14);
        _tooltipPanel.Position = new Vector2(
            Mathf.Min(proposed.X, Mathf.Max(0, Size.X - _tooltipPanel.Size.X - 4)),
            Mathf.Min(proposed.Y, Mathf.Max(0, Size.Y - _tooltipPanel.Size.Y - 4)));
    }

    public bool SelectFactionAt(Vector2 localPosition)
    {
        if (_preset is null || _draft is null) return false;
        var index = NearestSite(localPosition, CalculateMapBounds(), true);
        if (index < 0) return false;
        _selectedSiteIndex = index;
        FactionPicked?.Invoke(_cities[index].FactionId);
        return true;
    }

    private int NearestSite(Vector2 pointer, Rect2 bounds, bool selectableOnly)
    {
        var nearestIndex = -1;
        var nearestDistance = float.MaxValue;
        for (var index = 0; index < _cities.Count; index++)
        {
            var city = _cities[index];
            if (selectableOnly && !city.Selectable) continue;
            var distance = Project(city.Tile, bounds).DistanceTo(pointer);
            if (distance >= nearestDistance) continue;
            nearestDistance = distance;
            nearestIndex = index;
        }
        var threshold = Mathf.Clamp(Mathf.Min(bounds.Size.X / 70f, bounds.Size.Y / 38f), 9f, 15f);
        return nearestDistance <= threshold ? nearestIndex : -1;
    }

    private Rect2 CalculateMapBounds()
    {
        var available = new Rect2(new Vector2(12, 12),
            new Vector2(Mathf.Max(1, Size.X - 24), Mathf.Max(1, Size.Y - 24)));
        if (_preset is null) return available;
        var mapAspect = _preset.Width / (float)_preset.Height;
        var availableAspect = available.Size.X / available.Size.Y;
        var displaySize = available.Size;
        if (availableAspect > mapAspect) displaySize.X = available.Size.Y * mapAspect;
        else displaySize.Y = available.Size.X / mapAspect;
        return new Rect2(available.Position + (available.Size - displaySize) / 2f, displaySize);
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
