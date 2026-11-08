using Godot;

namespace SanguoSLG.Game;

public sealed partial class EditMapScene
{
    private enum MapEditorTool
    {
        Select,
        Paint,
        Facility,
    }

    private const float WorkspaceHeaderHeight = 68f;
    private const float WorkspaceSidebarWidth = 306f;
    private const float WorkspaceGap = 12f;
    private const float WorkspaceWorkbarHeight = 40f;
    private const float WorkspaceStatusHeight = 38f;

    private MapEditorTool _activeTool = MapEditorTool.Select;
    private readonly Dictionary<MapEditorTool, Button> _toolButtons = [];
    private readonly Dictionary<char, Button> _terrainPaletteButtons = [];
    private Label _workspaceMapLabel = null!;
    private Label _workspaceToolLabel = null!;
    private Label _workspaceTerrainLabel = null!;
    private Label _workspaceZoomLabel = null!;

    private void BuildWorkspaceInterface()
    {
        _font = GD.Load<FontFile>("res://assets/fonts/Pretendard-SemiBold.otf");
        Theme = GeneralWorkspaceStyle.Theme();
        ConfigureMapViewportRect();
        BuildWorkspaceHeader();
        BuildWorkspaceSidebar();
        BuildWorkspaceWorkbar();
        BuildWorkspaceStatusbar();
        SetEditorTool(MapEditorTool.Select);
        RefreshWorkspaceInformation();
    }

    private void ConfigureMapViewportRect()
    {
        _mapViewportContainer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _mapViewportContainer.OffsetLeft = WorkspaceSidebarWidth + WorkspaceGap * 2f;
        _mapViewportContainer.OffsetTop = WorkspaceHeaderHeight + WorkspaceWorkbarHeight + WorkspaceGap * 2f;
        _mapViewportContainer.OffsetRight = -WorkspaceGap;
        _mapViewportContainer.OffsetBottom = -(WorkspaceStatusHeight + WorkspaceGap);
    }

    private void BuildWorkspaceHeader()
    {
        var header = new PanelContainer { Name = "MapEditorHeader", MouseFilter = MouseFilterEnum.Stop };
        header.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        header.OffsetLeft = WorkspaceGap;
        header.OffsetTop = WorkspaceGap;
        header.OffsetRight = -WorkspaceGap;
        header.OffsetBottom = WorkspaceHeaderHeight;
        header.AddThemeStyleboxOverride("panel", GeneralWorkspaceStyle.SectionBox());
        AddChild(header);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 9);
        header.AddChild(row);
        var mark = new Label
        {
            Text = "三國",
            CustomMinimumSize = new Vector2(62, 0),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        mark.AddThemeFontSizeOverride("font_size", 22);
        mark.AddThemeColorOverride("font_color", new Color("f1c878"));
        row.AddChild(mark);
        row.AddChild(new VSeparator());
        var heading = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        heading.AddThemeConstantOverride("separation", 0);
        row.AddChild(heading);
        var title = new Label { Text = "시나리오 맵 에디터" };
        title.AddThemeFontSizeOverride("font_size", 20);
        heading.AddChild(title);
        var subtitle = new Label { Text = "지형과 거점을 편집하고 새 게임용 지도를 관리합니다." };
        subtitle.AddThemeFontSizeOverride("font_size", 12);
        subtitle.AddThemeColorOverride("font_color", new Color("bda581"));
        heading.AddChild(subtitle);

        AddHeaderButton(row, "되돌리기", UndoDraft, 92);
        AddHeaderButton(row, "다시 적용", RedoDraft, 96);
        AddSaveDraftButton(row);
    }

    private static void AddHeaderButton(HBoxContainer row, string text, Action action, float width)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(width, 36) };
        button.Pressed += action;
        row.AddChild(button);
    }

    private void BuildWorkspaceSidebar()
    {
        var panel = new PanelContainer { Name = "MapEditorSidebar", MouseFilter = MouseFilterEnum.Stop };
        panel.AnchorBottom = 1f;
        panel.OffsetLeft = WorkspaceGap;
        panel.OffsetTop = WorkspaceHeaderHeight + WorkspaceGap;
        panel.OffsetRight = WorkspaceSidebarWidth + WorkspaceGap;
        panel.OffsetBottom = -(WorkspaceStatusHeight + WorkspaceGap);
        panel.AddThemeStyleboxOverride("panel", GeneralWorkspaceStyle.SectionBox());
        AddChild(panel);

        var scroll = new ScrollContainer
        {
            Name = "MapEditorSidebarScroll",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
        };
        GeneralWorkspaceStyle.StyleScrollContainer(scroll, 7);
        panel.AddChild(scroll);
        var margin = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        margin.AddThemeConstantOverride("margin_right", 9);
        scroll.AddChild(margin);
        var content = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 9);
        margin.AddChild(content);

        AddSidebarHeading(content, "편집 도구");
        var toolGroup = new ButtonGroup();
        AddToolButton(content, toolGroup, MapEditorTool.Select, "선택", "타일 정보와 편집 명령을 확인합니다.");
        AddToolButton(content, toolGroup, MapEditorTool.Paint, "지형 그리기", "선택한 지형을 왼쪽 드래그로 칠합니다.");
        AddToolButton(content, toolGroup, MapEditorTool.Facility, "시설물", "기존 시설물 명령을 열어 편집합니다.");

        content.AddChild(new HSeparator());
        AddSidebarHeading(content, "지형 팔레트");
        var palette = new GridContainer
        {
            Name = "TerrainPalette",
            Columns = 2,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        palette.AddThemeConstantOverride("h_separation", 7);
        palette.AddThemeConstantOverride("v_separation", 7);
        content.AddChild(palette);
        var terrainGroup = new ButtonGroup();
        _terrainButtons.Clear();
        foreach (var (code, entry) in Terrain)
        {
            var button = new Button
            {
                Text = entry.Name,
                ToggleMode = true,
                ButtonGroup = terrainGroup,
                FocusMode = Control.FocusModeEnum.None,
                CustomMinimumSize = new Vector2(124, 34),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                TooltipText = $"{entry.Name} 지형을 그리기 브러시로 선택",
            };
            button.AddThemeStyleboxOverride("normal", FilterStyle(false));
            button.AddThemeStyleboxOverride("hover", PaletteHoverStyle());
            button.AddThemeStyleboxOverride("pressed", FilterStyle(true));
            button.Pressed += () => SelectPaintTerrain(code);
            palette.AddChild(button);
            _terrainButtons.Add(button);
            _terrainPaletteButtons[code] = button;
            if (code == _paintCode) button.ButtonPressed = true;
        }

        content.AddChild(new HSeparator());
        AddSidebarHeading(content, "표시 및 지역 이동");
        var routesToggle = new CheckButton { Name = "LandRouteToggle", Text = "육상 동선 표시", ButtonPressed = true };
        routesToggle.Toggled += enabled =>
        {
            _showLandRoutes = enabled;
            if (_routeLines is not null) _routeLines.Visible = enabled;
            QueueRedraw();
        };
        content.AddChild(routesToggle);
        var citiesToggle = new CheckButton { Name = "CityToggle", Text = $"성·시설 표시 ({_cities.Count})", ButtonPressed = true };
        citiesToggle.Toggled += enabled =>
        {
            _showCities = enabled;
            if (_mapViewport is not null) RefreshVisibleTerrain();
            QueueRedraw();
        };
        content.AddChild(citiesToggle);
        var regions = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        regions.AddThemeConstantOverride("h_separation", 7);
        regions.AddThemeConstantOverride("v_separation", 7);
        content.AddChild(regions);
        foreach (var (label, longitude, latitude) in new[]
        {
            ("중국", 113.0, 34.0), ("한반도", 127.5, 37.0),
            ("대만", 121.0, 23.7), ("일본", 130.7, 33.3),
        })
        {
            var button = new Button { Text = label, TooltipText = $"{label} 지역으로 이동" };
            button.Pressed += () => FocusRegion(longitude, latitude);
            regions.AddChild(button);
        }
    }

    private static void AddSidebarHeading(VBoxContainer parent, string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 16);
        label.AddThemeColorOverride("font_color", new Color("f1c878"));
        parent.AddChild(label);
    }

    private void AddToolButton(VBoxContainer parent, ButtonGroup group, MapEditorTool tool, string text, string tooltip)
    {
        var button = new Button
        {
            Text = text,
            ToggleMode = true,
            ButtonGroup = group,
            CustomMinimumSize = new Vector2(0, 38),
            TooltipText = tooltip,
        };
        button.Pressed += () => SetEditorTool(tool);
        parent.AddChild(button);
        _toolButtons[tool] = button;
    }

    private void BuildWorkspaceWorkbar()
    {
        var panel = new PanelContainer { Name = "MapWorkspaceBar", MouseFilter = MouseFilterEnum.Stop };
        panel.AnchorRight = 1f;
        panel.OffsetLeft = WorkspaceSidebarWidth + WorkspaceGap * 2f;
        panel.OffsetTop = WorkspaceHeaderHeight + WorkspaceGap;
        panel.OffsetRight = -WorkspaceGap;
        panel.OffsetBottom = WorkspaceHeaderHeight + WorkspaceGap + WorkspaceWorkbarHeight;
        panel.AddThemeStyleboxOverride("panel", WorkspaceBarStyle());
        AddChild(panel);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        panel.AddChild(row);
        _workspaceMapLabel = AddWorkbarLabel(row, "지도: 기본 지도", true);
        row.AddChild(new VSeparator());
        _workspaceToolLabel = AddWorkbarLabel(row, "도구: 선택");
        row.AddChild(new VSeparator());
        _workspaceTerrainLabel = AddWorkbarLabel(row, "브러시: 평야");
        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        _workspaceZoomLabel = AddWorkbarLabel(row, "확대 100%");
    }

    private static Label AddWorkbarLabel(HBoxContainer row, string text, bool expand = false)
    {
        var label = new Label
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = expand ? Control.SizeFlags.ExpandFill : Control.SizeFlags.ShrinkBegin,
        };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", new Color("ead3ac"));
        row.AddChild(label);
        return label;
    }

    private void BuildWorkspaceStatusbar()
    {
        var panel = new PanelContainer { Name = "MapWorkspaceStatus", MouseFilter = MouseFilterEnum.Ignore };
        panel.AnchorRight = 1f;
        panel.AnchorTop = 1f;
        panel.AnchorBottom = 1f;
        panel.OffsetLeft = WorkspaceSidebarWidth + WorkspaceGap * 2f;
        panel.OffsetTop = -(WorkspaceStatusHeight + WorkspaceGap);
        panel.OffsetRight = -WorkspaceGap;
        panel.OffsetBottom = -WorkspaceGap;
        panel.AddThemeStyleboxOverride("panel", WorkspaceBarStyle());
        AddChild(panel);
        _tileInfo = new Label
        {
            Text = _presetLoadError is null
                ? "지도 위 타일을 선택하세요.  ·  가운데 드래그/WASD: 이동  ·  휠: 확대/축소"
                : "프리셋 로드 실패: " + _presetLoadError,
            VerticalAlignment = VerticalAlignment.Center,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
        };
        _tileInfo.AddThemeFontSizeOverride("font_size", 13);
        _tileInfo.AddThemeColorOverride("font_color", new Color("d7c5ab"));
        panel.AddChild(_tileInfo);
    }

    private void SetEditorTool(MapEditorTool tool)
    {
        if (_painting) EndPaintStroke();
        _activeTool = tool;
        _paintMode = tool == MapEditorTool.Paint;
        foreach (var (kind, button) in _toolButtons) button.ButtonPressed = kind == tool;
        if (_editorPalette is not null)
        {
            _editorPalette.QueueFree();
            _editorPalette = null;
        }
        if (_selectionVisual is not null) _selectionVisual.Visible = false;
        RefreshWorkspaceInformation();
        _tileInfo.Text = tool switch
        {
            MapEditorTool.Paint => $"지형 그리기 · {Terrain[_paintCode].Name} · 좌클릭 드래그: 변경 · 가운데 드래그/WASD: 이동",
            MapEditorTool.Facility => "시설물 · 타일을 클릭하여 기존 성·항구·관문·유적·생산시설 명령을 엽니다.",
            _ => "선택 · 타일을 클릭하여 좌표와 편집 명령을 확인합니다.",
        };
    }

    private void SelectPaintTerrain(char code)
    {
        _paintCode = code;
        foreach (var (kind, button) in _terrainPaletteButtons) button.ButtonPressed = kind == code;
        RefreshWorkspaceInformation();
        if (_paintMode)
            _tileInfo.Text = $"지형 그리기 · {Terrain[code].Name} · 좌클릭 드래그로 지형을 변경합니다.";
    }

    private void RefreshWorkspaceInformation()
    {
        if (_workspaceMapLabel is null) return;
        _workspaceMapLabel.Text = $"지도: {_activeMapVersion?.Title ?? "기본 지도"}";
        _workspaceToolLabel.Text = $"도구: {ToolName(_activeTool)}";
        _workspaceTerrainLabel.Text = $"브러시: {Terrain[_paintCode].Name}";
        _workspaceZoomLabel.Text = $"확대 {Mathf.RoundToInt(30f / _viewSize * 100f)}%";
    }

    private static string ToolName(MapEditorTool tool) => tool switch
    {
        MapEditorTool.Paint => "지형 그리기",
        MapEditorTool.Facility => "시설물",
        _ => "선택",
    };

    private static StyleBoxFlat WorkspaceBarStyle()
    {
        var style = GeneralWorkspaceStyle.SectionBox();
        style.SetContentMargin(Side.Left, 12);
        style.SetContentMargin(Side.Right, 12);
        style.SetContentMargin(Side.Top, 7);
        style.SetContentMargin(Side.Bottom, 7);
        return style;
    }

    private static StyleBoxFlat PaletteHoverStyle()
    {
        var style = FilterStyle(false);
        style.BgColor = new Color("4a2a1d");
        style.BorderColor = new Color("c6a05b");
        return style;
    }
}
