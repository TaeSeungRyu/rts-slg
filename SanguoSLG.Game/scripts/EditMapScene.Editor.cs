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
    private int _nextSiteId = 1;

    private void InitializeEditorSites()
    {
        var id = 1;
        foreach (var city in _cities)
            _sites.Add(new MapEditorSite(id++, city.Name, "city", city.Size, city.Tile.X, city.Tile.Y));
        _nextSiteId = id;
    }

    private static bool IsLand(char code) => code is 'P' or 'F' or 'M' or 'D' or 'B';
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
        var offsets = site.Kind == "city" ? CastleFootprint.OffsetsFor(CastleKind(site.Size))
            : site.Kind == "port" && site.Size == "medium"
                ? FeatureFootprint.OffsetsFor(FeatureType.PortMedium)
                : [new HexCoord(0, 0)];
        return offsets.Select(offset => Offset(axial + offset));
    }

    private MapEditorSite? SiteAt(Vector2I tile) =>
        _sites.FirstOrDefault(site => Footprint(site).Contains(tile));

    private bool CanPlace(MapEditorSite candidate)
    {
        var cells = Footprint(candidate).ToArray();
        return cells.All(cell => Inside(cell) && IsLand(_rows[cell.Y][cell.X]) &&
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
        palette.AddThemeStyleboxOverride("panel", PanelStyle());
        AddChild(palette);
        _editorPalette = palette;
        palette.Position = new Vector2(Mathf.Clamp(screen.X + 12f, 6f, Mathf.Max(6f, Size.X - 245f)),
            Mathf.Clamp(screen.Y + 12f, 118f, Mathf.Max(118f, Size.Y - 340f)));
        palette.CustomMinimumSize = new Vector2(232, 0);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 5);
        palette.AddChild(content);
        content.AddChild(new Label { Text = $"{Terrain[terrain].Name}  ·  {tile.X}, {tile.Y}" });
        content.AddChild(new HSeparator());

        void AddCommand(string label, bool enabled, string reason, Action action)
        {
            var button = new Button { Text = label, Disabled = !enabled,
                TooltipText = enabled ? label : reason,
                CustomMinimumSize = new Vector2(210, 32) };
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
            () => _tileInfo.Text = "지형 변경 창을 준비합니다.");
        AddCommand("항구 관리", site is null && IsLand(terrain) && HasNeighborWater(tile),
            "물과 접한 빈 육상 타일을 선택하세요.",
            () => _tileInfo.Text = "항구 관리 창을 준비합니다.");
        AddCommand("항구 편집", site?.Kind == "port", "항구를 선택하세요.",
            () => _tileInfo.Text = "항구 편집 창을 준비합니다.");
        AddCommand("닫기", true, "", () =>
        {
            palette.QueueFree();
            _editorPalette = null;
            _selectedTile = null;
        });
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
        panel.AddThemeStyleboxOverride("panel", PanelStyle());
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
        var name = new LineEdit { Text = existing?.Name ?? "", PlaceholderText = "성 이름을 입력하세요" };
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
            var trimmed = name.Text.Trim();
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
            if (existing is not null) _sites.Remove(existing);
            _sites.Add(candidate);
            if (existing is null) _nextSiteId++;
            RefreshSiteVisuals();
            CloseEditorModal();
            ShowEditorPalette(tile, _editorPalette?.Position ?? new Vector2(20, 130));
        };
        if (existing is not null)
        {
            var remove = new Button { Text = "성 제거" };
            remove.Pressed += () =>
            {
                _sites.Remove(existing);
                _sites.RemoveAll(site => site.ParentCityId == existing.Id);
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
            if (existing is not null) _sites.Remove(existing);
            _sites.Add(candidate);
            if (existing is null) _nextSiteId++;
            RefreshSiteVisuals();
            CloseEditorModal();
            ShowEditorPalette(tile, _editorPalette?.Position ?? new Vector2(20, 130));
        };
        if (existing is not null)
        {
            var remove = new Button { Text = "시설 제거" };
            remove.Pressed += () =>
            {
                _sites.Remove(existing);
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
