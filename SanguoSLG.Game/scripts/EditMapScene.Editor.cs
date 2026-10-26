using Godot;
using SanguoSLG.Core.Data;

namespace SanguoSLG.Game;

public sealed partial class EditMapScene
{
    private readonly List<MapEditorSite> _sites = [];
    private Vector2I? _selectedTile;
    private PanelContainer? _editorPalette;

    private void InitializeEditorSites()
    {
        var id = 1;
        foreach (var city in _cities)
            _sites.Add(new MapEditorSite(id++, city.Name, "city", city.Size, city.Tile.X, city.Tile.Y));
    }

    private static bool IsLand(char code) => code is 'P' or 'F' or 'M' or 'D' or 'B';
    private bool Inside(Vector2I tile) => tile.X >= 0 && tile.X < _width && tile.Y >= 0 && tile.Y < _height;
    private MapEditorSite? SiteAt(Vector2I tile) =>
        _sites.FirstOrDefault(site => site.Column == tile.X && site.Row == tile.Y);

    private bool HasNeighborWater(Vector2I tile) => Axial(tile).Neighbors()
        .Select(Offset)
        .Any(neighbor => Inside(neighbor) && _rows[neighbor.Y][neighbor.X] is 'W' or 'O');

    private bool HasNearbyCity(Vector2I tile) => _sites.Any(site => site.Kind == "city" &&
        Axial(tile).Distance(Axial(new Vector2I(site.Column, site.Row))) <= 2);

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
            () => _tileInfo.Text = "성 추가 편집 창을 준비합니다.");
        AddCommand("성 편집", site?.Kind == "city", "성을 선택하세요.",
            () => _tileInfo.Text = "성 편집 창을 준비합니다.");
        AddCommand("생산 시설 관리", site is null && IsLand(terrain) && HasNearbyCity(tile),
            "성에서 2칸 이내의 빈 육상 타일을 선택하세요.",
            () => _tileInfo.Text = "생산 시설 관리 창을 준비합니다.");
        AddCommand("시설 편집", site?.Kind == "facility", "생산 시설을 선택하세요.",
            () => _tileInfo.Text = "시설 편집 창을 준비합니다.");
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
}
