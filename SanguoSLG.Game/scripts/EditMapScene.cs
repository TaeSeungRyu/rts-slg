using System.Text.Json;
using Godot;

namespace SanguoSLG.Game;

public sealed partial class EditMapScene : Control
{
    private sealed record MapCity(string Name, string Region, string Size, Vector2I Tile);

    private const float Radius = 8f;
    private const float StepX = Radius * 1.5f;
    private static readonly float StepY = Radius * Mathf.Sqrt(3f);
    private static readonly Dictionary<char, (string Name, Color Color)> Terrain = new()
    {
        ['P'] = ("평야", new Color("9dbb83")),
        ['F'] = ("숲", new Color("3f8065")),
        ['M'] = ("산악", new Color("829079")),
        ['H'] = ("험산", new Color("686d69")),
        ['D'] = ("사막", new Color("d4bd82")),
        ['R'] = ("하천", new Color("5d9eb8")),
        ['B'] = ("큰강 나루", new Color("b5ad8a")),
        ['W'] = ("연안", new Color("72adbc")),
        ['O'] = ("해역", new Color("3b6c87")),
    };
    private readonly Vector2[] _hex = Enumerable.Range(0, 6)
        .Select(index => new Vector2(Mathf.Cos(Mathf.Pi / 3f * index) * Radius,
            Mathf.Sin(Mathf.Pi / 3f * index) * Radius)).ToArray();
    private string[] _rows = [];
    private int _width;
    private int _height;
    private double _west;
    private double _east;
    private double _south;
    private double _north;
    private float _scale;
    private Vector2 _pan;
    private bool _dragging;
    private Vector2 _dragLast;
    private Label _tileInfo = null!;
    private char? _terrainFilter;
    private readonly List<Button> _terrainButtons = [];
    private readonly List<Vector2I[]> _landRoutes = [];
    private readonly List<MapCity> _cities = [];
    private bool _showLandRoutes = true;
    private bool _showCities = true;
    private FontFile _font = null!;

    public override void _Ready()
    {
        using var document = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(
            "res://assets/maps/east-asia-map.json"));
        var root = document.RootElement;
        _width = root.GetProperty("width").GetInt32();
        _height = root.GetProperty("height").GetInt32();
        _rows = root.GetProperty("rows").EnumerateArray().Select(row => row.GetString() ?? "").ToArray();
        foreach (var route in root.GetProperty("land_routes").EnumerateArray())
            _landRoutes.Add(route.GetProperty("tiles").EnumerateArray().Select(tile =>
                new Vector2I(tile[0].GetInt32(), tile[1].GetInt32())).ToArray());
        foreach (var city in root.GetProperty("cities").EnumerateArray())
        {
            var tile = city.GetProperty("tile");
            _cities.Add(new MapCity(city.GetProperty("name").GetString() ?? "",
                city.GetProperty("region").GetString() ?? "",
                city.GetProperty("size").GetString() ?? "",
                new Vector2I(tile[0].GetInt32(), tile[1].GetInt32())));
        }
        var bounds = root.GetProperty("bounds");
        _west = bounds.GetProperty("west").GetDouble();
        _east = bounds.GetProperty("east").GetDouble();
        _south = bounds.GetProperty("south").GetDouble();
        _north = bounds.GetProperty("north").GetDouble();
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        BuildInterface();
        CallDeferred(nameof(FitMap));
        if (OS.GetCmdlineUserArgs().Contains("--editmapqa")) CallDeferred(nameof(RunQa));
    }

    private void BuildInterface()
    {
        _font = GD.Load<FontFile>("res://assets/fonts/Pretendard-SemiBold.otf");
        var font = _font;
        var bar = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
        bar.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        bar.CustomMinimumSize = new Vector2(0, 115);
        bar.AddThemeStyleboxOverride("panel", PanelStyle());
        AddChild(bar);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 7);
        bar.AddChild(content);
        var title = new Label { Text = "동아시아 시나리오 지형  ·  중국 / 몽골 / 한반도 / 대만 / 일본 서부" };
        title.AddThemeFontOverride("font", font);
        title.AddThemeFontSizeOverride("font_size", 20);
        content.AddChild(title);
        var legend = new Label { Text = "지형을 선택하면 해당 타일을 강조합니다.    │    휠: 확대  ·  드래그: 이동  ·  클릭: 지형 확인",
            Modulate = new Color("d9d1b0") };
        legend.AddThemeFontOverride("font", font);
        legend.AddThemeFontSizeOverride("font_size", 13);
        content.AddChild(legend);
        var filters = new HBoxContainer();
        filters.AddThemeConstantOverride("separation", 6);
        content.AddChild(filters);
        var group = new ButtonGroup();
        AddTerrainFilter("전체", null);
        foreach (var (code, entry) in Terrain)
            AddTerrainFilter(entry.Name, code);
        var routesToggle = new CheckButton { Name = "LandRouteToggle", Text = "육상 동선",
            ButtonPressed = true, CustomMinimumSize = new Vector2(112, 26) };
        routesToggle.AddThemeFontOverride("font", font);
        routesToggle.AddThemeFontSizeOverride("font_size", 12);
        routesToggle.Toggled += enabled => { _showLandRoutes = enabled; QueueRedraw(); };
        filters.AddChild(routesToggle);
        var citiesToggle = new CheckButton { Name = "CityToggle", Text = $"성 배치 {_cities.Count}",
            ButtonPressed = true, CustomMinimumSize = new Vector2(115, 26) };
        citiesToggle.AddThemeFontOverride("font", font);
        citiesToggle.AddThemeFontSizeOverride("font_size", 12);
        citiesToggle.Toggled += enabled => { _showCities = enabled; QueueRedraw(); };
        filters.AddChild(citiesToggle);
        void AddTerrainFilter(string name, char? code)
        {
            var button = new Button { Text = name, ToggleMode = true, ButtonGroup = group,
                CustomMinimumSize = new Vector2(72, 26), FocusMode = FocusModeEnum.None };
            button.AddThemeFontOverride("font", font);
            button.AddThemeFontSizeOverride("font_size", 12);
            button.AddThemeStyleboxOverride("normal", FilterStyle(false));
            button.AddThemeStyleboxOverride("hover", FilterStyle(false));
            button.AddThemeStyleboxOverride("pressed", FilterStyle(true));
            button.Pressed += () => { _terrainFilter = code; QueueRedraw(); };
            filters.AddChild(button);
            _terrainButtons.Add(button);
            if (code is null) button.ButtonPressed = true;
        }
        var footer = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        footer.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
        footer.CustomMinimumSize = new Vector2(0, 39);
        footer.AddThemeStyleboxOverride("panel", PanelStyle());
        AddChild(footer);
        _tileInfo = new Label { Text = "성을 클릭하면 지역과 규모를, 지형을 클릭하면 타일 정보를 표시합니다." };
        _tileInfo.AddThemeFontOverride("font", font);
        _tileInfo.AddThemeFontSizeOverride("font_size", 14);
        footer.AddChild(_tileInfo);
    }

    private static StyleBoxFlat PanelStyle()
    {
        var style = new StyleBoxFlat { BgColor = new Color("182126"),
            BorderColor = new Color("bfa56b") };
        style.SetBorderWidthAll(1);
        style.SetContentMarginAll(10);
        return style;
    }

    private static StyleBoxFlat FilterStyle(bool selected)
    {
        var style = new StyleBoxFlat { BgColor = selected ? new Color("876b35") : new Color("2a3638"),
            BorderColor = selected ? new Color("e5ca83") : new Color("60706c") };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(4);
        style.SetContentMarginAll(4);
        return style;
    }

    private Vector2 Center(int column, int row) =>
        new(column * StepX, row * StepY + (column % 2) * StepY * 0.5f);

    private void FitMap()
    {
        var mapWidth = (_width - 1) * StepX + Radius * 2;
        var mapHeight = (_height + 0.5f) * StepY;
        _scale = Mathf.Min((Size.X - 30) / mapWidth, (Size.Y - 185) / mapHeight);
        _pan = new Vector2((Size.X - mapWidth * _scale) * 0.5f,
            125 + (Size.Y - 185 - mapHeight * _scale) * 0.5f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("183949"));
        DrawSetTransform(_pan, 0f, Vector2.One * _scale);
        for (var row = 0; row < _height; row++)
        for (var column = 0; column < _width; column++)
        {
            var center = Center(column, row);
            var points = _hex.Select(vertex => center + vertex).ToArray();
            var kind = _rows[row][column];
            var baseColor = Terrain[kind].Color;
            var color = _terrainFilter is null ? baseColor
                : _terrainFilter == kind ? baseColor.Lerp(Colors.White, 0.30f)
                : baseColor.Lerp(new Color("172126"), 0.64f);
            DrawColoredPolygon(points, color);
            if (_scale > 1.25f)
                DrawPolyline([.. points, points[0]], new Color(0f, 0f, 0f, 0.11f), 0.55f);
            if (_scale > 1.45f && kind is 'M' or 'H')
                DrawLine(center + new Vector2(-3, 2), center + new Vector2(0, -3),
                    new Color(0.92f, 0.92f, 0.85f, 0.55f), 0.8f);
            if (_scale > 1.45f && kind == 'F')
                DrawCircle(center, 1.8f, new Color("275c4d"));
        }
        if (_showLandRoutes)
        {
            foreach (var route in _landRoutes)
            {
                var points = route.Select(tile => Center(tile.X, tile.Y)).ToArray();
                DrawPolyline(points, new Color("24352c"), 3.2f, true);
                DrawPolyline(points, new Color("f2e2a2"), 1.5f, true);
            }
        }
        if (_showCities)
        {
            foreach (var city in _cities)
            {
                var center = Center(city.Tile.X, city.Tile.Y);
                var radius = CityRadius(city);
                var color = CityColor(city);
                DrawCircle(center + new Vector2(0, 1.5f), radius + 2f, new Color("131c1e"));
                DrawCircle(center, radius + 1f, new Color("e5d19d"));
                DrawCircle(center, radius - 0.6f, color);
                var roof = new[] { center + new Vector2(-radius * 0.65f, -0.6f),
                    center + new Vector2(0, -radius * 0.66f),
                    center + new Vector2(radius * 0.65f, -0.6f) };
                DrawColoredPolygon(roof, new Color("fff0d3"));
                DrawRect(new Rect2(center + new Vector2(-radius * 0.45f, 0),
                    new Vector2(radius * 0.9f, radius * 0.5f)), new Color("fff0d3"));
            }
        }
        DrawSetTransform(Vector2.Zero);
        if (_showCities)
        {
            foreach (var city in _cities)
            {
                var minimumScale = city.Size == "large" ? 0.5f : city.Size == "medium" ? 0.9f : 1.3f;
                if (_scale < minimumScale) continue;
                var center = _pan + Center(city.Tile.X, city.Tile.Y) * _scale;
                var labelPosition = center + new Vector2(-39, -(CityRadius(city) + 4) * _scale);
                DrawStringOutline(_font, labelPosition, city.Name, HorizontalAlignment.Center,
                    78, 12, 3, new Color("10191a"));
                DrawString(_font, labelPosition, city.Name, HorizontalAlignment.Center,
                    78, 12, new Color("fff2d8"));
            }
        }
    }

    private static float CityRadius(MapCity city) => city.Size switch
    {
        "large" => 9f,
        "medium" => 7f,
        _ => 5.5f,
    };

    private static Color CityColor(MapCity city) => city.Region switch
    {
        "대한민국" => new Color("658eb0"),
        "대만" => new Color("bd8d62"),
        "일본" => new Color("b57c91"),
        _ => new Color("a28452"),
    };

    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventMouseButton button)
        {
            if (button.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown && button.Pressed)
            {
                var previous = _scale;
                _scale = Mathf.Clamp(_scale * (button.ButtonIndex == MouseButton.WheelUp ? 1.15f : 1f / 1.15f), 0.35f, 4f);
                _pan = button.Position - (button.Position - _pan) * (_scale / previous);
                QueueRedraw();
                AcceptEvent();
            }
            else if (button.ButtonIndex is MouseButton.Left or MouseButton.Middle)
            {
                _dragging = button.Pressed;
                _dragLast = button.Position;
                if (!button.Pressed && button.ButtonIndex == MouseButton.Left) SelectTile(button.Position);
                AcceptEvent();
            }
        }
        else if (input is InputEventMouseMotion motion && _dragging)
        {
            _pan += motion.Position - _dragLast;
            _dragLast = motion.Position;
            QueueRedraw();
            AcceptEvent();
        }
    }

    private void SelectTile(Vector2 screen)
    {
        if (_showCities)
        {
            var city = _cities.OrderBy(item => screen.DistanceTo(
                _pan + Center(item.Tile.X, item.Tile.Y) * _scale)).FirstOrDefault();
            if (city != null && screen.DistanceTo(
                    _pan + Center(city.Tile.X, city.Tile.Y) * _scale)
                <= Mathf.Max(8f, (CityRadius(city) + 2f) * _scale))
            {
                var sizeName = city.Size switch
                {
                    "large" => "대형성",
                    "medium" => "중형성",
                    _ => "소형성",
                };
                _tileInfo.Text = $"{city.Name}  │  {city.Region}  │  {sizeName}";
                return;
            }
        }
        var point = (screen - _pan) / _scale;
        var column = Mathf.RoundToInt(point.X / StepX);
        if (column < 0 || column >= _width) return;
        var row = Mathf.RoundToInt((point.Y - (column % 2) * StepY * 0.5f) / StepY);
        if (row < 0 || row >= _height || point.DistanceTo(Center(column, row)) > Radius * 1.05f) return;
        var lon = _west + (column + 0.5) * (_east - _west) / _width;
        var lat = _north - (row + 0.5) * (_north - _south) / _height;
        _tileInfo.Text = $"{Terrain[_rows[row][column]].Name}    │    {lon:F1}°E, {lat:F1}°N    │    타일 {column}, {row}";
    }

    private void RunQa()
    {
        bool Land(double lon, double lat)
        {
            var column = Math.Clamp((int)((lon - _west) / (_east - _west) * _width), 0, _width - 1);
            var row = Math.Clamp((int)((_north - lat) / (_north - _south) * _height), 0, _height - 1);
            return _rows[row][column] is not ('O' or 'W');
        }
        var valid = _rows.Length == _height && _rows.All(row => row.Length == _width)
            && Land(110, 35) && Land(103, 47) && Land(127.5, 37.5)
            && Land(121, 24) && Land(130.5, 33.5)
            && _rows.Any(row => row.Contains('R')) && _rows.Any(row => row.Contains('D'))
            && _rows.Any(row => row.Contains('F')) && _rows.Any(row => row.Contains('H'))
            && _west >= 97.72 && _east <= 133
            && _terrainButtons.Count == Terrain.Count + 1 && _terrainButtons[0].ButtonPressed
            && _landRoutes.Count >= 10 && _landRoutes.All(route => route.Length >= 2
                && route.All(tile => tile.X >= 0 && tile.X < _width && tile.Y >= 0 && tile.Y < _height
                    && "PFMDRB".Contains(_rows[tile.Y][tile.X])))
            && _cities.Count == 36
            && _cities.All(city => city.Tile.X >= 0 && city.Tile.X < _width
                && city.Tile.Y >= 0 && city.Tile.Y < _height
                && "PFMD".Contains(_rows[city.Tile.Y][city.Tile.X]));
        _terrainButtons.First(button => button.Text == "산악").EmitSignal(Button.SignalName.Pressed);
        valid &= _terrainFilter == 'M';
        var routesToggle = FindChild("LandRouteToggle", true, false) as CheckButton;
        valid &= routesToggle?.ButtonPressed == true && _showLandRoutes;
        var citiesToggle = FindChild("CityToggle", true, false) as CheckButton;
        valid &= citiesToggle?.ButtonPressed == true && _showCities;
        var capital = _cities.FirstOrDefault(city => city.Name == "한성");
        if (capital != null)
        {
            SelectTile(_pan + Center(capital.Tile.X, capital.Tile.Y) * _scale);
            valid &= _tileInfo.Text.Contains("한성") && _tileInfo.Text.Contains("중형성");
        }
        else valid = false;
        GD.Print($"[edit-map-qa] passed={valid} tiles={_width * _height} bounds={_west}-{_east}E,{_south}-{_north}N");
        GetTree().Quit(valid ? 0 : 1);
    }
}
