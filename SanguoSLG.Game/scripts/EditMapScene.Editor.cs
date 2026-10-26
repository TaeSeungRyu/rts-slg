using Godot;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

public sealed partial class EditMapScene
{
    private readonly List<MapEditorSite> _sites = [];
    private Vector2I? _selectedTile;
    private PanelContainer? _editorPalette;
    private Control? _editorModal;
    private Label? _editorError;
    private Action? _editorCancelAction;
    private int _nextSiteId = 1;
    private bool _draftChanged;
    private Button? _saveDraftButton;
    private string? _presetLoadError;
    private readonly Stack<ScenarioMapPreset> _draftUndo = [];
    private readonly Stack<ScenarioMapPreset> _draftRedo = [];

    private void InitializeEditorSites()
    {
        var id = 1;
        foreach (var city in _cities)
            _sites.Add(new MapEditorSite(id++, city.Name, "city", city.Size, city.Tile.X, city.Tile.Y));
        _nextSiteId = id;
        NormalizeBaseCityFootprints();
        foreach (var port in _defaultPorts)
        {
            var candidate = Enumerable.Range(Mathf.Max(0, port.Column - 10),
                    Mathf.Min(_width - 1, port.Column + 10) - Mathf.Max(0, port.Column - 10) + 1)
                .SelectMany(column => Enumerable.Range(Mathf.Max(0, port.Row - 10),
                    Mathf.Min(_height - 1, port.Row + 10) - Mathf.Max(0, port.Row - 10) + 1)
                    .SelectMany(row => Enumerable.Range(0, 6)
                        .Select(direction => port with { Column = column, Row = row, Direction = direction })))
                .Where(site => CanPlace(site) && PortFacesWater(site))
                .OrderBy(site => (site.Column - port.Column) * (site.Column - port.Column) +
                    (site.Row - port.Row) * (site.Row - port.Row))
                .ThenBy(site => site.Row).ThenBy(site => site.Column)
                .FirstOrDefault();
            if (candidate is not null) _sites.Add(candidate);
        }
        _nextSiteId = _sites.Count == 0 ? 1 : _sites.Max(site => site.Id) + 1;
        var path = PresetPath();
        if (!File.Exists(path)) return;
        try
        {
            var preset = ScenarioMapPresetStore.Load(path);
            _width = preset.Width;
            _height = preset.Height;
            _rows = preset.Rows.ToArray();
            _sites.Clear();
            _sites.AddRange(preset.Sites);
            _nextSiteId = _sites.Count == 0 ? 1 : _sites.Max(site => site.Id) + 1;
        }
        catch (Exception error)
        {
            _presetLoadError = error.Message;
        }
    }

    private void NormalizeBaseCityFootprints()
    {
        foreach (var city in _sites.ToArray())
        {
            if (CanPlace(city)) continue;
            var origin = Axial(new Vector2I(city.Column, city.Row));
            var replacement = Enumerable.Range(Mathf.Max(0, city.Column - 5),
                    Mathf.Min(_width - 1, city.Column + 5) - Mathf.Max(0, city.Column - 5) + 1)
                .SelectMany(column => Enumerable.Range(Mathf.Max(0, city.Row - 5),
                    Mathf.Min(_height - 1, city.Row + 5) - Mathf.Max(0, city.Row - 5) + 1)
                    .Select(row => city with { Column = column, Row = row }))
                .Where(CanPlace)
                .OrderBy(candidate => origin.Distance(Axial(new Vector2I(candidate.Column, candidate.Row))))
                .ThenBy(candidate => candidate.Row).ThenBy(candidate => candidate.Column)
                .FirstOrDefault();
            if (replacement is null) continue;
            _sites.Remove(city);
            _sites.Add(replacement);
            var oldCity = _cities.FindIndex(item => item.Name == city.Name);
            if (oldCity >= 0) _cities[oldCity] = _cities[oldCity] with
            {
                Tile = new Vector2I(replacement.Column, replacement.Row),
            };
        }
    }

    private static string PresetPath() => OS.GetCmdlineUserArgs().Contains("--editmapqa")
        ? Path.Combine(Path.GetTempPath(), $"editmap-qa-{System.Environment.ProcessId}.json")
        : ProjectSettings.GlobalizePath("user://map-presets/east-asia.json");

    private ScenarioMapPreset Draft() => new(1, _width, _height, _rows.ToArray(), _sites.ToArray());

    private void MarkDraftChanged()
    {
        _draftChanged = true;
        if (_saveDraftButton is not null) _saveDraftButton.Text = "맵 저장 *";
    }

    private void RecordChange(ScenarioMapPreset before)
    {
        _draftUndo.Push(before);
        _draftRedo.Clear();
        MarkDraftChanged();
    }

    private void RestoreDraft(ScenarioMapPreset snapshot)
    {
        _rows = snapshot.Rows.ToArray();
        _sites.Clear();
        _sites.AddRange(snapshot.Sites);
        _nextSiteId = _sites.Count == 0 ? 1 : _sites.Max(site => site.Id) + 1;
        RefreshSiteVisuals();
        MarkDraftChanged();
        if (_selectedTile is { } selected)
            ShowEditorPalette(selected, _editorPalette?.Position ?? new Vector2(20, 130));
    }

    private void SaveDraft()
    {
        try
        {
            ScenarioMapPresetStore.Save(PresetPath(), Draft());
            _draftChanged = false;
            _saveDraftButton!.Text = "맵 저장";
            _tileInfo.Text = "시나리오 맵 프리셋을 저장했습니다. 새 게임에만 적용됩니다.";
        }
        catch (Exception error)
        {
            _tileInfo.Text = "맵 저장 실패: " + error.Message;
        }
    }

    private void AddSaveDraftButton(HBoxContainer bar)
    {
        if (ScenarioStartMenu.FromStartMenu)
        {
            var back = new Button { Text = "시작 화면", CustomMinimumSize = new Vector2(95, 26) };
            back.Pressed += () => ScenarioStartMenu.ReturnToStart(GetTree());
            bar.AddChild(back);
        }
        _saveDraftButton = new Button { Text = "맵 저장", CustomMinimumSize = new Vector2(95, 26) };
        _saveDraftButton.Pressed += SaveDraft;
        bar.AddChild(_saveDraftButton);
    }

    private static bool IsLand(char code) => code is 'P' or 'F' or 'M' or 'D' or 'B' or 'S' or 'T' or 'N' or 'Q';
    private bool Inside(Vector2I tile) => tile.X >= 0 && tile.X < _width && tile.Y >= 0 && tile.Y < _height;
    private static CastleSize CastleKind(string size) => size switch
    {
        "large" => CastleSize.Large,
        "medium" => CastleSize.Medium,
        _ => CastleSize.Small,
    };

    private IEnumerable<Vector2I> Footprint(MapEditorSite site)
    {
        var center = new Vector2I(site.Column, site.Row);
        var axial = Axial(center);
        var offsets = site.Kind == "landmark"
            ? FeatureFootprint.OffsetsFor(FeatureType.WaterfallCliff)
            : site.Kind == "city" ? CastleFootprint.OffsetsFor(CastleKind(site.Size))
            : site.Kind == "port" && site.Size == "medium"
                ? FeatureFootprint.OffsetsFor(FeatureType.PortMedium)
                : [new HexCoord(0, 0)];
        return offsets.Select(offset => Offset(axial + CastleFootprint.Rotate(offset, site.Direction)));
    }

    private MapEditorSite? SiteAt(Vector2I tile) =>
        _sites.FirstOrDefault(site => Footprint(site).Contains(tile));

    private bool CanPlace(MapEditorSite candidate)
    {
        var cells = Footprint(candidate).ToArray();
        return cells.All(cell => Inside(cell) &&
            (candidate.Kind == "landmark" ? IsLand(_rows[cell.Y][cell.X]) || _rows[cell.Y][cell.X] is 'H' or 'X' or 'C'
                : IsLand(_rows[cell.Y][cell.X])) &&
            !_sites.Where(site => site.Id != candidate.Id).Any(site => Footprint(site).Contains(cell)));
    }

    private bool HasNeighborWater(Vector2I tile) => Axial(tile).Neighbors()
        .Select(Offset)
        .Any(neighbor => Inside(neighbor) && _rows[neighbor.Y][neighbor.X] is 'W' or 'O');

    private IReadOnlyList<MapEditorSite> NearbyCities(Vector2I tile) => _sites
        .Where(site => site.Kind == "city" && Footprint(site)
            .Any(cell => Axial(tile).Distance(Axial(cell)) <= 2))
        .OrderBy(site => site.Id).ToArray();

    private bool HasNearbyCity(Vector2I tile) => NearbyCities(tile).Count > 0;

    private void ShowEditorPalette(Vector2I tile, Vector2 screen)
    {
        _selectedTile = tile;
        ShowSelectedTile(tile);
        if (_editorPalette is not null)
        {
            _editorPalette.QueueFree();
            _editorPalette = null;
        }
        var site = SiteAt(tile);
        var terrain = _rows[tile.Y][tile.X];
        _tileInfo.Text = $"{Terrain[terrain].Name}  │  타일 {tile.X}, {tile.Y}" +
            (site is null ? "" : $"  │  {site.Name} ({site.Size})");

        var palette = new PanelContainer { Name = "MapEditorPalette", MouseFilter = MouseFilterEnum.Stop };
        palette.AddThemeStyleboxOverride("panel", EditorFrame(new Color("1b0f0d"), 2));
        AddChild(palette);
        _editorPalette = palette;
        palette.Position = new Vector2(Mathf.Clamp(screen.X + 12f, 6f, Mathf.Max(6f, Size.X - 245f)),
            Mathf.Clamp(screen.Y + 12f, 118f, Mathf.Max(118f, Size.Y - 340f)));
        palette.CustomMinimumSize = new Vector2(232, 0);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 5);
        palette.AddChild(content);
        content.AddChild(new Label { Text = $"{Terrain[terrain].Name}  ·  {tile.X}, {tile.Y}" });
        var terrainInfo = terrain switch
        {
            'W' or 'O' => "수면 · 육상 부대 진입 불가",
            'H' => "험산 · 일반 육상 부대 진입 불가",
            'R' => "소하천 · 이동속도 감소",
            'B' => "나루 · 강 횡단 가능",
            'M' => "산악 · 이동속도 감소",
            _ => "육상 지형 · 부대 이동 가능",
        };
        content.AddChild(new Label { Text = terrainInfo, Modulate = new Color("eee0bf") });
        if (site is not null)
        {
            var kindName = site.Kind switch { "city" => "성", "port" => "항구", "landmark" => "폭포 절벽", _ => "생산 시설" };
            content.AddChild(new Label { Text = $"{kindName}: {site.Name} · 방향 {site.Direction + 1}/6",
                Modulate = new Color("e8c475") });
        }
        content.AddChild(new HSeparator());

        void AddCommand(string label, bool enabled, string reason, Action action)
        {
            var button = new Button { Text = label, Disabled = !enabled,
                TooltipText = enabled ? label : reason,
                CustomMinimumSize = new Vector2(210, 32) };
            button.AddThemeFontOverride("font", _font);
            button.AddThemeColorOverride("font_color", new Color("eee0bf"));
            button.AddThemeColorOverride("font_hover_color", new Color("ffe08a"));
            button.AddThemeStyleboxOverride("normal", EditorFrame(new Color("302018"), 1));
            button.AddThemeStyleboxOverride("hover", EditorFrame(new Color("66291c"), 1));
            button.AddThemeStyleboxOverride("pressed", EditorFrame(new Color("98291f"), 1));
            button.Pressed += action;
            content.AddChild(button);
        }

        AddCommand("성 추가", site is null && IsLand(terrain), "빈 육상 타일에서만 추가할 수 있습니다.",
            () => OpenCityEditor(tile, null));
        AddCommand("성 편집", site?.Kind == "city", "성을 선택하세요.",
            () => OpenCityEditor(tile, site));
        AddCommand("생산 시설 관리", site is null && IsLand(terrain) && HasNearbyCity(tile),
            "성에서 2칸 이내의 빈 육상 타일을 선택하세요.",
            () => OpenFacilityEditor(tile, null));
        AddCommand("시설 편집", site?.Kind == "facility", "생산 시설을 선택하세요.",
            () => OpenFacilityEditor(tile, site));
        AddCommand("지형 변경", site is null, "배치물이 있는 타일은 바꿀 수 없습니다.",
            () => OpenTerrainEditor(tile));
        AddCommand("항구 관리", site is null && IsLand(terrain) && HasNeighborWater(tile),
            "물과 접한 빈 육상 타일을 선택하세요.",
            () => OpenPortEditor(tile, null));
        AddCommand("항구 편집", site?.Kind == "port", "항구를 선택하세요.",
            () => OpenPortEditor(tile, site));
        AddCommand("폭포 절벽 추가", site is null && CanPlace(new MapEditorSite(_nextSiteId,
                "폭포 절벽", "landmark", "waterfall_cliff", tile.X, tile.Y)),
            "3칸 발자국이 육상에 있고 다른 배치물과 겹치지 않아야 합니다.",
            () => PlaceWaterfall(tile));
        AddCommand("폭포 절벽 제거", site?.Kind == "landmark", "폭포 절벽을 선택하세요.",
            () => RemoveWaterfall(tile, site));
        AddCommand("닫기", true, "", () =>
        {
            palette.QueueFree();
            _editorPalette = null;
            _selectedTile = null;
            _selectionVisual.Visible = false;
        });
    }

    private static StyleBoxFlat EditorFrame(Color background, int border)
    {
        var frame = new StyleBoxFlat { BgColor = background, BorderColor = new Color("d1ac60") };
        frame.SetBorderWidthAll(border);
        frame.SetCornerRadiusAll(5);
        frame.SetContentMarginAll(6);
        return frame;
    }

    private void PlaceWaterfall(Vector2I tile)
    {
        var site = new MapEditorSite(_nextSiteId, "폭포 절벽", "landmark", "waterfall_cliff", tile.X, tile.Y);
        if (!CanPlace(site)) return;
        var before = Draft();
        _sites.Add(site);
        _nextSiteId++;
        RecordChange(before);
        RefreshSiteVisuals();
        ShowEditorPalette(tile, _editorPalette?.Position ?? new Vector2(20, 130));
    }

    private void RemoveWaterfall(Vector2I tile, MapEditorSite? site)
    {
        if (site?.Kind != "landmark") return;
        var before = Draft();
        _sites.Remove(site);
        RecordChange(before);
        RefreshSiteVisuals();
        ShowEditorPalette(tile, _editorPalette?.Position ?? new Vector2(20, 130));
    }

    private VBoxContainer OpenEditorModal(string title)
    {
        CloseEditorModal();
        var backdrop = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.62f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);
        _editorModal = backdrop;
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        backdrop.AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(420, 0) };
        panel.AddThemeStyleboxOverride("panel", EditorFrame(new Color("1b0f0d"), 2));
        center.AddChild(panel);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 9);
        panel.AddChild(content);
        var heading = new HBoxContainer();
        content.AddChild(heading);
        heading.AddChild(new Label { Text = title, SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var close = new Button { Text = "✕" };
        close.Pressed += CloseEditorModal;
        heading.AddChild(close);
        content.AddChild(new HSeparator());
        _editorError = new Label { Text = "", Modulate = new Color("f0a486") };
        content.AddChild(_editorError);
        return content;
    }

    private void CloseEditorModal()
    {
        var cancel = _editorCancelAction;
        _editorCancelAction = null;
        cancel?.Invoke();
        _editorModal?.QueueFree();
        _editorModal = null;
        _editorError = null;
    }

    private static OptionButton SelectOptions(VBoxContainer content, string label, string[] options, int selected)
    {
        content.AddChild(new Label { Text = label });
        var picker = new OptionButton();
        foreach (var option in options) picker.AddItem(option);
        picker.Selected = selected;
        content.AddChild(picker);
        return picker;
    }

    private void OpenCityEditor(Vector2I tile, MapEditorSite? existing)
    {
        var content = OpenEditorModal(existing is null ? "성 추가" : "성 편집");
        content.AddChild(new Label { Text = "성 이름" });
        var name = new TextEdit { Text = existing?.Name ?? "", PlaceholderText = "성 이름을 입력하세요",
            CustomMinimumSize = new Vector2(0f, 42f), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        name.AddThemeFontOverride("font", _font);
        content.AddChild(name);
        var sizes = new[] { "small", "medium", "large" };
        var size = SelectOptions(content, "성 종류", ["소형성", "중형성", "대형성"],
            Math.Max(0, Array.IndexOf(sizes, existing?.Size ?? "small")));
        var direction = SelectOptions(content, "성 방향", ["동", "남동", "남서", "서", "북서", "북동"],
            existing?.Direction ?? 0);
        var actions = new HBoxContainer();
        content.AddChild(actions);
        var save = new Button { Text = "적용", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddChild(save);
        save.Pressed += () =>
        {
            name.ApplyIme();
            var trimmed = name.Text.Replace("\r", "").Replace("\n", "").Trim();
            if (trimmed.Length == 0 || _sites.Any(site => site.Kind == "city" &&
                    site.Id != existing?.Id && site.Name == trimmed))
            {
                _editorError!.Text = "비어 있거나 중복된 성 이름입니다.";
                return;
            }
            var candidate = new MapEditorSite(existing?.Id ?? _nextSiteId, trimmed, "city",
                sizes[size.Selected], existing?.Column ?? tile.X, existing?.Row ?? tile.Y, direction.Selected);
            if (!CanPlace(candidate))
            {
                _editorError!.Text = "성 발자국이 지도 밖이거나 다른 배치물과 겹칩니다.";
                return;
            }
            var before = Draft();
            if (existing is not null) _sites.Remove(existing);
            _sites.Add(candidate);
            if (existing is null) _nextSiteId++;
            RecordChange(before);
            RefreshSiteVisuals();
            CloseEditorModal();
            ShowEditorPalette(tile, _editorPalette?.Position ?? new Vector2(20, 130));
        };
        if (existing is not null)
        {
            var remove = new Button { Text = "성 제거" };
            remove.Pressed += () =>
            {
                var before = Draft();
                _sites.Remove(existing);
                _sites.RemoveAll(site => site.ParentCityId == existing.Id);
                RecordChange(before);
                RefreshSiteVisuals();
                CloseEditorModal();
                ShowEditorPalette(tile, _editorPalette?.Position ?? new Vector2(20, 130));
            };
            actions.AddChild(remove);
        }
        var cancel = new Button { Text = "취소" };
        cancel.Pressed += CloseEditorModal;
        actions.AddChild(cancel);
    }

    private void RefreshSiteVisuals()
    {
        foreach (var visual in _visibleTiles.Values) visual.QueueFree();
        _visibleTiles.Clear();
        foreach (var visual in _visibleCities.Values) visual.QueueFree();
        _visibleCities.Clear();
        foreach (var visual in _visibleStructures.Values) visual.QueueFree();
        _visibleStructures.Clear();
        RefreshVisibleTerrain();
    }

    private void OpenFacilityEditor(Vector2I tile, MapEditorSite? existing)
    {
        var cityOptions = NearbyCities(tile);
        if (cityOptions.Count == 0 && existing is null) return;
        var content = OpenEditorModal(existing is null ? "생산 시설 추가" : "시설 편집");
        var kinds = new[] { "paddy", "farm", "village", "workshop" };
        var kind = SelectOptions(content, "시설 종류", ["논", "밭", "마을", "공방"],
            Math.Max(0, Array.IndexOf(kinds, existing?.Size ?? "paddy")));
        var direction = SelectOptions(content, "방향", ["동", "남동", "남서", "서", "북서", "북동"],
            existing?.Direction ?? 0);
        OptionButton? city = null;
        if (existing is null)
            city = SelectOptions(content, "연결 성 (경계에서 2칸)",
                cityOptions.Select(option => option.Name).ToArray(), 0);
        else
            content.AddChild(new Label { Text = "연결 성: " +
                (_sites.FirstOrDefault(site => site.Id == existing.ParentCityId)?.Name ?? "없음") });
        var actions = new HBoxContainer();
        content.AddChild(actions);
        var apply = new Button { Text = "적용", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddChild(apply);
        apply.Pressed += () =>
        {
            var owner = existing?.ParentCityId ?? cityOptions[city!.Selected].Id;
            var candidate = new MapEditorSite(existing?.Id ?? _nextSiteId, kinds[kind.Selected] + " " +
                (existing?.Id ?? _nextSiteId), "facility", kinds[kind.Selected],
                existing?.Column ?? tile.X, existing?.Row ?? tile.Y, direction.Selected, owner);
            if (!CanPlace(candidate))
            {
                _editorError!.Text = "시설이 다른 배치물과 겹치거나 지형이 적합하지 않습니다.";
                return;
            }
            var before = Draft();
            if (existing is not null) _sites.Remove(existing);
            _sites.Add(candidate);
            if (existing is null) _nextSiteId++;
            RecordChange(before);
            RefreshSiteVisuals();
            CloseEditorModal();
            ShowEditorPalette(tile, _editorPalette?.Position ?? new Vector2(20, 130));
        };
        if (existing is not null)
        {
            var remove = new Button { Text = "시설 제거" };
            remove.Pressed += () =>
            {
                var before = Draft();
                _sites.Remove(existing);
                RecordChange(before);
                RefreshSiteVisuals();
                CloseEditorModal();
                ShowEditorPalette(tile, _editorPalette?.Position ?? new Vector2(20, 130));
            };
            actions.AddChild(remove);
        }
        var cancel = new Button { Text = "취소" };
        cancel.Pressed += CloseEditorModal;
        actions.AddChild(cancel);
    }

    private void SetTerrain(Vector2I tile, char code)
    {
        var chars = _rows[tile.Y].ToCharArray();
        chars[tile.X] = code;
        _rows[tile.Y] = new string(chars);
        if (_visibleTiles.Remove(tile, out var visual)) visual.QueueFree();
        foreach (var neighbor in Axial(tile).Neighbors().Select(Offset).Where(Inside))
            if (_visibleTiles.Remove(neighbor, out var neighborVisual)) neighborVisual.QueueFree();
        RefreshVisibleTerrain();
        QueueRedraw();
    }

    private bool CanChangeTerrain(Vector2I tile, char code)
    {
        if (SiteAt(tile) is not null) return false;
        if (code is 'W' or 'O' or 'H' && _landRoutes.Any(route => route.Contains(tile))) return false;
        var original = _rows[tile.Y][tile.X];
        SetTerrain(tile, code);
        var valid = _sites.Where(site => site.Kind == "port").All(site =>
            HasNeighborWater(new Vector2I(site.Column, site.Row)));
        SetTerrain(tile, original);
        return valid;
    }

    private void OpenTerrainEditor(Vector2I tile)
    {
        var content = OpenEditorModal("지형 변경");
        var codes = Terrain.Keys.ToArray();
        var original = _rows[tile.Y][tile.X];
        _editorCancelAction = () => SetTerrain(tile, original);
        var terrain = SelectOptions(content, "타일 지형", codes.Select(code => Terrain[code].Name).ToArray(),
            Array.IndexOf(codes, original));
        var preview = new SubViewportContainer
        {
            Stretch = true,
            CustomMinimumSize = new Vector2(420, 170),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        content.AddChild(preview);
        var viewport = new SubViewport
        {
            Size = new Vector2I(420, 170),
            TransparentBg = false,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        };
        preview.AddChild(viewport);
        var previewWorld = new Node3D();
        viewport.AddChild(previewWorld);
        previewWorld.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color("8aac9a"),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = Colors.White,
                AmbientLightEnergy = 0.8f,
            },
        });
        previewWorld.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-55f, -30f, 0f) });
        var camera = new Camera3D { Current = true, Projection = Camera3D.ProjectionType.Orthogonal, Size = 2.7f };
        previewWorld.AddChild(camera);
        camera.Position = new Vector3(0f, 2.3f, 3f);
        camera.LookAt(Vector3.Zero);
        Node3D? previewTile = null;
        var description = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(420, 48), Modulate = new Color("e8d9b4") };
        content.AddChild(description);
        void UpdatePreview(char code)
        {
            previewTile?.QueueFree();
            previewTile = TerrainScene(code).Instantiate<Node3D>();
            if (code == 'R') MapView3D.TintRiverTile(previewTile);
            previewWorld.AddChild(previewTile);
            description.Text = TerrainDescription(code);
        }
        UpdatePreview(original);
        terrain.ItemSelected += index =>
        {
            var code = codes[(int)index];
            if (!CanChangeTerrain(tile, code))
            {
                _editorError!.Text = "배치물 또는 항구의 해안 조건 때문에 바꿀 수 없습니다.";
                terrain.Selected = Array.IndexOf(codes, _rows[tile.Y][tile.X]);
                return;
            }
            _editorError!.Text = "";
            SetTerrain(tile, code);
            UpdatePreview(code);
        };
        content.AddChild(new Label { Text = "종류를 고르면 화면에 즉시 미리 보입니다." });
        var actions = new HBoxContainer();
        content.AddChild(actions);
        var apply = new Button { Text = "적용", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        apply.Pressed += () =>
        {
            var current = _rows[tile.Y][tile.X];
            if (current != original)
            {
                var previousRows = _rows.ToArray();
                var chars = previousRows[tile.Y].ToCharArray();
                chars[tile.X] = original;
                previousRows[tile.Y] = new string(chars);
                RecordChange(Draft() with { Rows = previousRows });
            }
            _editorCancelAction = null;
            CloseEditorModal();
            ShowEditorPalette(tile, _editorPalette?.Position ?? new Vector2(20, 130));
        };
        actions.AddChild(apply);
        var cancel = new Button { Text = "취소" };
        cancel.Pressed += CloseEditorModal;
        actions.AddChild(cancel);
    }

    private static string TerrainDescription(char code) => code switch
    {
        'P' => "기본 육상 지형. 부대와 도시를 배치할 수 있습니다.",
        'F' => "숲 지형. 이동이 가능하고 전투에서 지형 효과를 받습니다.",
        'M' => "소형산. 육상 이동이 가능하지만 속도가 느려집니다.",
        'H' or 'X' or 'C' or 'I' or 'E' or 'L' => "험준한 지형. 일반 부대가 통과할 수 없습니다.",
        'D' or 'Q' => "사막 지형. 육상 이동이 가능하며 지형 효과를 받습니다.",
        'R' => "소하천. 이웃 강 타일 방향에 맞춰 물길 에셋이 연결됩니다.",
        'B' => "큰강 나루. 강을 가로지르는 육상 통로입니다.",
        'W' or 'O' => "수면 지형. 선박만 이동할 수 있습니다.",
        'V' => "물가 암초. 이동 가능한 수면이 아니므로 항로에서 피해야 합니다.",
        'N' => "늪. 육상 이동이 가능하지만 속도가 느려집니다.",
        'S' or 'T' => "바위 지형. 육상 이동이 가능합니다.",
        _ => "지형을 선택하면 실제 게임 에셋을 미리 볼 수 있습니다.",
    };

    private void UndoDraft()
    {
        if (!_draftUndo.TryPop(out var before)) return;
        _draftRedo.Push(Draft());
        RestoreDraft(before);
    }

    private void RedoDraft()
    {
        if (!_draftRedo.TryPop(out var after)) return;
        _draftUndo.Push(Draft());
        RestoreDraft(after);
    }

    private bool PortFacesWater(MapEditorSite site)
    {
        var directions = new[] { new HexCoord(1, 0), new HexCoord(0, 1),
            new HexCoord(-1, 1), new HexCoord(-1, 0), new HexCoord(0, -1), new HexCoord(1, -1) };
        return Footprint(site).Any(cell =>
        {
            var adjacent = Offset(Axial(cell) + directions[site.Direction]);
            return Inside(adjacent) && _rows[adjacent.Y][adjacent.X] is 'W' or 'O';
        });
    }

    private void OpenPortEditor(Vector2I tile, MapEditorSite? existing)
    {
        var content = OpenEditorModal(existing is null ? "항구 추가" : "항구 편집");
        content.AddChild(new Label { Text = "항구 이름" });
        var name = new TextEdit { Text = existing?.Name ?? "", PlaceholderText = "항구 이름을 입력하세요",
            CustomMinimumSize = new Vector2(0f, 42f), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        name.AddThemeFontOverride("font", _font);
        content.AddChild(name);
        var sizes = new[] { "small", "medium" };
        var size = SelectOptions(content, "항구 종류", ["소형항", "중형항"],
            Math.Max(0, Array.IndexOf(sizes, existing?.Size ?? "small")));
        var firstValidDirection = existing?.Direction ?? Enumerable.Range(0, 6)
            .FirstOrDefault(direction => PortFacesWater(new MapEditorSite(0, "임시", "port", "small",
                tile.X, tile.Y, direction)));
        var directionPicker = SelectOptions(content, "부두 방향", ["동", "남동", "남서", "서", "북서", "북동"],
            firstValidDirection);
        var actions = new HBoxContainer();
        content.AddChild(actions);
        var apply = new Button { Text = "적용", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        apply.Pressed += () =>
        {
            name.ApplyIme();
            var trimmed = name.Text.Replace("\r", "").Replace("\n", "").Trim();
            if (trimmed.Length == 0 || _sites.Any(site => site.Kind == "port" &&
                    site.Id != existing?.Id && site.Name == trimmed))
            {
                _editorError!.Text = "비어 있거나 중복된 항구 이름입니다.";
                return;
            }
            var candidate = new MapEditorSite(existing?.Id ?? _nextSiteId, trimmed, "port",
                sizes[size.Selected], existing?.Column ?? tile.X, existing?.Row ?? tile.Y,
                directionPicker.Selected);
            if (!CanPlace(candidate) || !PortFacesWater(candidate))
            {
                _editorError!.Text = "항구 발자국이 겹치거나 부두가 물을 향하지 않습니다.";
                return;
            }
            var before = Draft();
            if (existing is not null) _sites.Remove(existing);
            _sites.Add(candidate);
            if (existing is null) _nextSiteId++;
            RecordChange(before);
            RefreshSiteVisuals();
            CloseEditorModal();
            ShowEditorPalette(tile, _editorPalette?.Position ?? new Vector2(20, 130));
        };
        actions.AddChild(apply);
        if (existing is not null)
        {
            var remove = new Button { Text = "항구 제거" };
            remove.Pressed += () =>
            {
                var before = Draft();
                _sites.Remove(existing);
                RecordChange(before);
                RefreshSiteVisuals();
                CloseEditorModal();
                ShowEditorPalette(tile, _editorPalette?.Position ?? new Vector2(20, 130));
            };
            actions.AddChild(remove);
        }
        var cancel = new Button { Text = "취소" };
        cancel.Pressed += CloseEditorModal;
        actions.AddChild(cancel);
    }
}
