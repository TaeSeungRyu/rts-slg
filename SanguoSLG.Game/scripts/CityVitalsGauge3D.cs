using Godot;

namespace SanguoSLG.Game;

/// <summary>성 위의 성벽·병력 게이지. 병력 막대의 10만 명은 표시 기준이며 주둔 상한이 아니다.</summary>
public sealed partial class CityVitalsGauge3D : Node3D
{
    private const int TroopDisplayReference = 100_000;
    private const int ViewWidth = 520;
    private const int ViewHeight = 198;
    public const float DisplayPixelSize = 0.0035f;

    private ProgressBar _wallBar = null!;
    private ProgressBar _troopBar = null!;
    private Label _title = null!;
    private Label _wallText = null!;
    private Label _troopText = null!;

    public double WallValue => _wallBar.Value;
    public double WallMaximum => _wallBar.MaxValue;
    public double TroopValue => _troopBar.Value;
    public string WallCaption => _wallText.Text;
    public string TroopCaption => _troopText.Text;
    public string Title => _title.Text;

    public override void _Ready()
    {
        var viewport = new SubViewport
        {
            Size = new Vector2I(ViewWidth, ViewHeight),
            TransparentBg = true,
            Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        };
        AddChild(viewport);
        var panel = new PanelContainer
        {
            Position = Vector2.Zero,
            Size = new Vector2(ViewWidth, ViewHeight),
        };
        var backing = new StyleBoxFlat { BgColor = new Color(0.04f, 0.055f, 0.065f, 0.82f),
            BorderColor = new Color("d7b86b") };
        backing.SetBorderWidthAll(2);
        backing.SetCornerRadiusAll(11);
        backing.SetContentMarginAll(9);
        panel.AddThemeStyleboxOverride("panel", backing);
        viewport.AddChild(panel);
        var rows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        rows.AddThemeConstantOverride("separation", 6);
        panel.AddChild(rows);
        _title = new Label { HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, CustomMinimumSize = new Vector2(0, 72) };
        _title.AddThemeFontSizeOverride("font_size", 32);
        _title.AddThemeColorOverride("font_color", new Color("fff2d5"));
        rows.AddChild(_title);
        (_wallBar, _wallText) = AddGauge(rows, "성벽", new Color("d7b86b"));
        (_troopBar, _troopText) = AddGauge(rows, "병력", new Color("75b8c9"));

        var billboard = new Sprite3D
        {
            Texture = viewport.GetTexture(),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            PixelSize = DisplayPixelSize,
            NoDepthTest = true,
            RenderPriority = 2,
        };
        AddChild(billboard);
    }

    public void SetValues(int wall, int maxWall, int troops)
    {
        _wallBar.MaxValue = Mathf.Max(1, maxWall);
        _wallBar.Value = Mathf.Clamp(wall, 0, maxWall);
        _wallText.Text = $"{wall:N0} / {maxWall:N0}";
        _troopBar.MaxValue = TroopDisplayReference;
        _troopBar.Value = Mathf.Clamp(troops, 0, TroopDisplayReference);
        _troopText.Text = $"{troops:N0}";
    }

    public void SetTitle(string title, Color color)
    {
        _title.Text = title;
        _title.AddThemeColorOverride("font_color", color);
    }

    private static (ProgressBar Bar, Label Text) AddGauge(VBoxContainer rows, string name, Color fill)
    {
        var line = new HBoxContainer { CustomMinimumSize = new Vector2(0, 45) };
        line.AddThemeConstantOverride("separation", 8);
        rows.AddChild(line);
        var caption = new Label
        {
            Text = name,
            CustomMinimumSize = new Vector2(52, 42),
            VerticalAlignment = VerticalAlignment.Center,
        };
        caption.AddThemeFontSizeOverride("font_size", 20);
        caption.AddThemeColorOverride("font_color", new Color("fff2d5"));
        caption.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
        caption.AddThemeConstantOverride("shadow_offset_x", 1);
        caption.AddThemeConstantOverride("shadow_offset_y", 1);
        line.AddChild(caption);
        var bar = new ProgressBar
        {
            CustomMinimumSize = new Vector2(210, 29),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            ShowPercentage = false,
        };
        var background = new StyleBoxFlat { BgColor = new Color("17202a"), BorderColor = new Color("dbc38c") };
        background.SetBorderWidthAll(1);
        background.SetCornerRadiusAll(5);
        bar.AddThemeStyleboxOverride("background", background);
        var foreground = new StyleBoxFlat { BgColor = fill };
        foreground.SetCornerRadiusAll(4);
        bar.AddThemeStyleboxOverride("fill", foreground);
        line.AddChild(bar);
        var value = new Label
        {
            Text = $"{name}  0",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
        };
        value.AddThemeFontSizeOverride("font_size", 21);
        value.AddThemeColorOverride("font_color", new Color("ffffff"));
        value.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
        value.AddThemeConstantOverride("shadow_offset_x", 1);
        value.AddThemeConstantOverride("shadow_offset_y", 1);
        line.AddChild(value);
        return (bar, value);
    }
}
