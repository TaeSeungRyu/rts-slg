using System.Text.Json;
using Godot;
using SanguoSLG.Core.Data;

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
        ['S'] = ("바위 무더기", new Color("8d9988")),
        ['T'] = ("바위 언덕", new Color("879080")),
        ['X'] = ("기암 석림", new Color("737d75")),
        ['C'] = ("절벽", new Color("77776e")),
        ['I'] = ("얼음산", new Color("b6d2cb")),
        ['E'] = ("큰 얼음벽", new Color("add1d2")),
        ['L'] = ("작은 얼음벽", new Color("abc5cc")),
        ['N'] = ("늪", new Color("718b74")),
        ['Q'] = ("선인장 사막", new Color("d0b67b")),
        ['V'] = ("물가 암초", new Color("7c9e9f")),
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
    private readonly List<MapEditorSite> _defaultPorts = [];
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
        var portId = _cities.Count + 1;
        foreach (var port in root.GetProperty("ports").EnumerateArray())
        {
            var tile = port.GetProperty("tile");
            _defaultPorts.Add(new MapEditorSite(portId++, port.GetProperty("name").GetString() ?? "",
                "port", port.GetProperty("size").GetString() ?? "small",
                tile[0].GetInt32(), tile[1].GetInt32(), port.GetProperty("direction").GetInt32()));
        }
        var bounds = root.GetProperty("bounds");
        _west = bounds.GetProperty("west").GetDouble();
        _east = bounds.GetProperty("east").GetDouble();
        _south = bounds.GetProperty("south").GetDouble();
        _north = bounds.GetProperty("north").GetDouble();
        InitializeEditorSites();
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Build3DView();
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
        bar.CustomMinimumSize = new Vector2(0, 145);
        bar.AddThemeStyleboxOverride("panel", PanelStyle());
        AddChild(bar);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 7);
        bar.AddChild(content);
        var title = new Label { Text = "시나리오 맵 에디터  ·  중국 / 몽골 / 한반도 / 대만 / 일본 서부" };
        title.AddThemeFontOverride("font", font);
        title.AddThemeFontSizeOverride("font_size", 20);
        content.AddChild(title);
        var legend = new Label { Text = "타일을 클릭해 편집 명령을 엽니다.    │    휠: 확대  ·  드래그/WASD/방향키: 이동",
            Modulate = new Color("d9d1b0") };
        legend.AddThemeFontOverride("font", font);
        legend.AddThemeFontSizeOverride("font_size", 13);
        content.AddChild(legend);
        var filterScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 31),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        content.AddChild(filterScroll);
        var filters = new HBoxContainer();
        filters.AddThemeConstantOverride("separation", 6);
        filterScroll.AddChild(filters);
        var group = new ButtonGroup();
        AddTerrainFilter("전체", null);
        foreach (var (code, entry) in Terrain)
            AddTerrainFilter(entry.Name, code);
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        content.AddChild(actions);
        var routesToggle = new CheckButton { Name = "LandRouteToggle", Text = "육상 동선",
            ButtonPressed = true, CustomMinimumSize = new Vector2(112, 26) };
        routesToggle.AddThemeFontOverride("font", font);
        routesToggle.AddThemeFontSizeOverride("font_size", 12);
        routesToggle.Toggled += enabled =>
        {
            _showLandRoutes = enabled;
            if (_routeLines is not null) _routeLines.Visible = enabled;
            QueueRedraw();
        };
        actions.AddChild(routesToggle);
        var citiesToggle = new CheckButton { Name = "CityToggle", Text = $"성 배치 {_cities.Count}",
            ButtonPressed = true, CustomMinimumSize = new Vector2(115, 26) };
        citiesToggle.AddThemeFontOverride("font", font);
        citiesToggle.AddThemeFontSizeOverride("font_size", 12);
        citiesToggle.Toggled += enabled =>
        {
            _showCities = enabled;
            if (_mapViewport is not null) RefreshVisibleTerrain();
            QueueRedraw();
        };
        actions.AddChild(citiesToggle);
        foreach (var (label, longitude, latitude) in new[]
        {
            ("중국", 113.0, 34.0), ("한반도", 127.5, 37.0),
            ("대만", 121.0, 23.7), ("일본", 130.7, 33.3),
        })
        {
            var regionButton = new Button { Text = label, TooltipText = $"{label} 지역으로 이동" };
            regionButton.Pressed += () => FocusRegion(longitude, latitude);
            actions.AddChild(regionButton);
        }
        var undo = new Button { Text = "되돌리기" };
        undo.Pressed += UndoDraft;
        actions.AddChild(undo);
        var redo = new Button { Text = "다시 적용" };
        redo.Pressed += RedoDraft;
        actions.AddChild(redo);
        AddSaveDraftButton(actions);
        void AddTerrainFilter(string name, char? code)
        {
            var button = new Button { Text = name, ToggleMode = true, ButtonGroup = group,
                CustomMinimumSize = new Vector2(72, 26), FocusMode = FocusModeEnum.None };
            button.AddThemeFontOverride("font", font);
            button.AddThemeFontSizeOverride("font_size", 12);
            button.AddThemeStyleboxOverride("normal", FilterStyle(false));
            button.AddThemeStyleboxOverride("hover", FilterStyle(false));
            button.AddThemeStyleboxOverride("pressed", FilterStyle(true));
            button.Pressed += () =>
            {
                _terrainFilter = code;
                ApplyTerrainFilterVisual();
                QueueRedraw();
            };
            filters.AddChild(button);
            _terrainButtons.Add(button);
            if (code is null) button.ButtonPressed = true;
        }
        var footer = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        footer.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
        footer.CustomMinimumSize = new Vector2(0, 39);
        footer.AddThemeStyleboxOverride("panel", PanelStyle());
        AddChild(footer);
        _tileInfo = new Label { Text = _presetLoadError is null
            ? "타일을 선택해 편집하세요. 저장한 지도는 새 게임에만 적용됩니다."
            : "프리셋 로드 실패: " + _presetLoadError };
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
        if (_mapViewport is not null) return;
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
        if (_mapViewport is not null)
        {
            Handle3DInput(input);
            return;
        }
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
            && _cities.Count == 68 && _defaultPorts.Count == 10
            && _sites.Count(site => site.Kind == "port") == 10
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
        valid &= _mapViewport is not null && _mapCamera.Current && _visibleTiles.Count > 100
            && _visibleTiles.Count < _width * _height;
        var centerTile = new Vector2I(_width / 2, _height / 2);
        valid &= _mapCamera.UnprojectPosition(_mapView.HexToWorld(Axial(centerTile + Vector2I.Up))).Y
            < _mapCamera.UnprojectPosition(_mapView.HexToWorld(Axial(centerTile + Vector2I.Down))).Y;
        valid &= _mapCamera.UnprojectPosition(_mapView.HexToWorld(Axial(centerTile + Vector2I.Right))).X
            > _mapCamera.UnprojectPosition(_mapView.HexToWorld(Axial(centerTile + Vector2I.Left))).X;
        valid &= Mathf.Abs(_mapCamera.RotationDegrees.Y - (_mapView.IsFlatTop ? 0f : 30f)) < 0.001f
            && Mathf.Abs(_mapCamera.RotationDegrees.Z) < 0.001f
            && Mathf.Abs(_mapCamera.RotationDegrees.X + 90f) < 0.001f;
        var squareCenter = _mapCamera.UnprojectPosition(_mapView.HexToWorld(Axial(centerTile)));
        var squareEast = _mapCamera.UnprojectPosition(_mapView.HexToWorld(Axial(centerTile + new Vector2I(2, 0))));
        var squareSouth = _mapCamera.UnprojectPosition(_mapView.HexToWorld(Axial(centerTile + new Vector2I(0, 2))));
        valid &= Mathf.Abs(squareEast.Y - squareCenter.Y) < 0.5f
            && Mathf.Abs(squareSouth.X - squareCenter.X) < 0.5f;
        var beforePan = _mapCamera.Position;
        var beforePanCenter = _viewCenter;
        PanMap(new Vector2(600, 400), new Vector2(604, 400));
        valid &= _mapCamera.Position.DistanceTo(beforePan) > 0.001f
            && _viewCenter == beforePanCenter;
        PanMap(new Vector2(604, 400), new Vector2(600, 400));
        valid &= _mapCamera.Position.DistanceTo(beforePan) < 0.01f;
        FocusRegion(127.5, 37.0);
        valid &= _viewCenter.X > _width * 3 / 4 && _visibleTiles.Count > 100;
        var koreaTile = _viewCenter;
        Select3DTile(_mapCamera.UnprojectPosition(_mapView.HexToWorld(Axial(koreaTile))));
        valid &= _tileInfo.Text.Contains($"{koreaTile.X}, {koreaTile.Y}");
        FocusRegion(130.7, 33.3);
        valid &= _viewCenter.X > _width * 4 / 5 && _visibleTiles.Count > 100;
        var japanTile = _viewCenter;
        Select3DTile(_mapCamera.UnprojectPosition(_mapView.HexToWorld(Axial(japanTile))));
        valid &= _tileInfo.Text.Contains($"{japanTile.X}, {japanTile.Y}");
        MoveMapCamera(new Vector3(10000f, 0f, 10000f));
        valid &= _viewCenter.X == _width - 1 && _viewCenter.Y == _height - 1;
        var southTile = _viewCenter;
        Select3DTile(_mapCamera.UnprojectPosition(_mapView.HexToWorld(Axial(southTile))));
        valid &= _tileInfo.Text.Contains($"{southTile.X}, {southTile.Y}");
        FocusRegion(113.0, 34.0);
        var visibleRiver = _visibleTiles.Keys.FirstOrDefault(tile => _rows[tile.Y][tile.X] is 'R' or 'B');
        if (_visibleTiles.ContainsKey(visibleRiver) && _rows[visibleRiver.Y][visibleRiver.X] is 'R' or 'B')
        {
            var (_, expectedRotation) = TerrainVisualFor(visibleRiver);
            valid &= Mathf.IsEqualApprox(_visibleTiles[visibleRiver].RotationDegrees.Y, expectedRotation);
        }
        var vacant = Enumerable.Range(0, _height)
            .SelectMany(row => Enumerable.Range(0, _width).Select(column => new Vector2I(column, row)))
            .FirstOrDefault(tile => _rows[tile.Y][tile.X] == 'P' && SiteAt(tile) is null);
        var draftCity = new SanguoSLG.Core.Data.MapEditorSite(_nextSiteId, "QA성", "city", "small",
            vacant.X, vacant.Y);
        valid &= CanPlace(draftCity);
        OpenCityEditor(vacant, null);
        var koreanNameInput = _editorModal?.FindChildren("*", "TextEdit", true, false)
            .OfType<TextEdit>().FirstOrDefault();
        valid &= koreanNameInput is not null;
        if (koreanNameInput is not null)
        {
            koreanNameInput.Text = "한";
            koreanNameInput.SetCaretColumn(1);
            koreanNameInput.InsertTextAtCaret("글시험성");
            _editorModal?.FindChildren("*", "Button", true, false).OfType<Button>()
                .First(button => button.Text == "적용").EmitSignal(Button.SignalName.Pressed);
            valid &= _sites.Any(site => site.Name == "한글시험성");
            UndoDraft();
        }
        OpenTerrainEditor(vacant);
        valid &= _editorModal is not null
            && _editorModal.FindChildren("*", "SubViewport", true, false).Count > 0
            && TerrainDescription('R').Contains("연결");
        CloseEditorModal();
        _sites.Add(draftCity);
        valid &= SiteAt(vacant)?.Id == draftCity.Id && !CanPlace(draftCity with { Id = _nextSiteId + 1 });
        _sites.Remove(draftCity);
        valid &= SiteAt(vacant) is null;
        var previousTerrain = _rows[vacant.Y][vacant.X];
        valid &= CanChangeTerrain(vacant, 'F');
        var beforeTerrain = Draft();
        SetTerrain(vacant, 'F');
        RecordChange(beforeTerrain);
        UndoDraft();
        valid &= _rows[vacant.Y][vacant.X] == previousTerrain;
        RedoDraft();
        valid &= _rows[vacant.Y][vacant.X] == 'F';
        UndoDraft();
        var coastal = Enumerable.Range(0, _height)
            .SelectMany(row => Enumerable.Range(0, _width).Select(column => new Vector2I(column, row)))
            .FirstOrDefault(tile => IsLand(_rows[tile.Y][tile.X]) && SiteAt(tile) is null
                && HasNeighborWater(tile));
        var portDraft = Enumerable.Range(0, 6)
            .Select(direction => new SanguoSLG.Core.Data.MapEditorSite(_nextSiteId, "QA항", "port",
                "small", coastal.X, coastal.Y, direction))
            .FirstOrDefault(PortFacesWater);
        valid &= portDraft is not null && CanPlace(portDraft);
        if (portDraft is not null && CanPlace(portDraft))
        {
            OpenPortEditor(coastal, null);
            var koreanPortInput = _editorModal?.FindChildren("*", "TextEdit", true, false)
                .OfType<TextEdit>().FirstOrDefault();
            valid &= koreanPortInput is not null;
            if (koreanPortInput is not null)
            {
                koreanPortInput.Text = "한";
                koreanPortInput.SetCaretColumn(1);
                koreanPortInput.InsertTextAtCaret("글시험항");
                _editorModal?.FindChildren("*", "Button", true, false).OfType<Button>()
                    .First(button => button.Text == "적용").EmitSignal(Button.SignalName.Pressed);
                valid &= _sites.Any(site => site.Name == "한글시험항");
                UndoDraft();
            }
        }
        valid &= _sites.Where(site => site.Kind == "city").All(CanPlace);
        ShowEditorPalette(vacant, new Vector2(200f, 200f));
        valid &= _editorPalette is not null
            && _editorPalette.FindChildren("*", "Button", true, false).OfType<Button>()
                .Any(button => button.Text == "성 추가" && !button.Disabled)
            && _editorPalette.FindChildren("*", "Button", true, false).OfType<Button>()
                .Any(button => button.Text == "성 편집" && button.Disabled);
        var qaPresetPath = Path.Combine(Path.GetTempPath(), $"editmap-qa-{System.Environment.ProcessId}.json");
        try
        {
            SaveDraft();
            var saved = SanguoSLG.Core.Data.ScenarioMapPresetStore.Load(qaPresetPath);
            valid &= saved.Width == _width && saved.Height == _height
                && saved.Sites.Count == _sites.Count && saved.Rows.SequenceEqual(_rows);
        }
        finally
        {
            if (File.Exists(qaPresetPath)) File.Delete(qaPresetPath);
        }
        GD.Print($"[edit-map-qa] passed={valid} ports={_sites.Count(site => site.Kind == "port")} tiles={_width * _height} visible={_visibleTiles.Count} bounds={_west}-{_east}E,{_south}-{_north}N");
        GetTree().Quit(valid ? 0 : 1);
    }
}
