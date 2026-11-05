using Godot;

namespace SanguoSLG.Game;

public sealed partial class ScenarioStartMenu
{
    private static readonly string[] TitleTips =
    [
        "보급로가 끊기면 강한 군대도 오래 싸울 수 없습니다.",
        "병종 적성과 장수의 특성을 함께 살펴보세요.",
        "강을 건너려면 선박과 출항 거점을 먼저 준비하세요.",
        "성벽이 무너지기 전에는 수성군이 유리합니다.",
        "정찰로 적의 병력과 성벽 상태를 확인할 수 있습니다.",
        "유적을 점령하면 새로운 병종을 생산할 수 있습니다.",
    ];

    private Label? _titleTip;
    private double _titleTipElapsed;
    private int _titleTipIndex;

    private TextureRect AddTitleBackdrop()
    {
        var darkness = new ColorRect { Color = new Color("1b171c"), MouseFilter = MouseFilterEnum.Ignore };
        darkness.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(darkness);

        var image = Image.LoadFromFile(ProjectSettings.GlobalizePath("res://assets/ui/title/red_cliffs_cleaned.png"));
        if (image is null || image.IsEmpty()) throw new InvalidDataException("시작 화면 이미지를 읽을 수 없습니다.");
        var original = ImageTexture.CreateFromImage(image);
        var cropped = new AtlasTexture { Atlas = original, Region = new Rect2(0, 0, 1000, 1000) };
        var illustration = new TextureRect
        {
            Name = "TitleIllustration",
            Texture = cropped,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        illustration.AnchorRight = 0.57f;
        illustration.AnchorBottom = 1;
        AddChild(illustration);

        var menuBackground = new ColorRect
        {
            Color = new Color(0.055f, 0.047f, 0.068f, 0.96f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        menuBackground.AnchorLeft = 0.55f;
        menuBackground.AnchorRight = 1;
        menuBackground.AnchorBottom = 1;
        AddChild(menuBackground);

        var embers = new CpuParticles2D
        {
            Name = "TitleEmbers",
            Amount = 52,
            Lifetime = 7,
            Emitting = true,
            Position = new Vector2(GetViewport().GetVisibleRect().Size.X * 0.47f,
                GetViewport().GetVisibleRect().Size.Y * 0.96f),
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = new Vector2(GetViewport().GetVisibleRect().Size.X * 0.33f, 35),
            Direction = new Vector2(0, -1),
            Spread = 35,
            InitialVelocityMin = 25,
            InitialVelocityMax = 95,
            Gravity = new Vector2(0, -6),
            ScaleAmountMin = 1.2f,
            ScaleAmountMax = 3.2f,
            Color = new Color(1f, 0.48f, 0.13f, 0.72f),
        };
        AddChild(embers);
        return illustration;
    }

    private static StyleBoxFlat TitleButtonStyle(Color background, Color border)
    {
        var style = new StyleBoxFlat { BgColor = background, BorderColor = border };
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(6);
        style.SetContentMarginAll(8);
        return style;
    }

    private void StyleTitleButton(Button button)
    {
        button.AddThemeColorOverride("font_color", new Color("f4dfb2"));
        button.AddThemeStyleboxOverride("normal", TitleButtonStyle(new Color("231b1a"), new Color("886638")));
        button.AddThemeStyleboxOverride("hover", TitleButtonStyle(new Color("684021"), new Color("ffcf73")));
        button.AddThemeStyleboxOverride("pressed", TitleButtonStyle(new Color("8c4d24"), new Color("fff0ac")));
        button.MouseEntered += () => CreateTween().TweenProperty(button, "modulate", new Color(1f, 0.9f, 0.68f), 0.13);
        button.MouseExited += () => CreateTween().TweenProperty(button, "modulate", Colors.White, 0.16);
    }

    private void AddTitleTips(VBoxContainer menu)
    {
        var tipFrame = new PanelContainer();
        var frame = TitleButtonStyle(new Color(0.025f, 0.025f, 0.04f, 0.82f), new Color("947247"));
        frame.BorderWidthLeft = 0;
        frame.BorderWidthRight = 0;
        tipFrame.AddThemeStyleboxOverride("panel", frame);
        menu.AddChild(tipFrame);
        _titleTip = new Label
        {
            Name = "TitleGameTip",
            Text = TitleTips[0],
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _titleTip.AddThemeColorOverride("font_color", new Color("e8d2a8"));
        tipFrame.AddChild(_titleTip);
    }

    private void AnimateTitleEntrance(TextureRect illustration, VBoxContainer menu)
    {
        illustration.PivotOffset = new Vector2(500, 500);
        var breathing = CreateTween().SetLoops();
        breathing.TweenProperty(illustration, "scale", new Vector2(1.018f, 1.018f), 10);
        breathing.TweenProperty(illustration, "scale", Vector2.One, 10);
        menu.Modulate = new Color(1, 1, 1, 0);
        var entrance = CreateTween();
        entrance.TweenInterval(0.2);
        entrance.TweenProperty(menu, "modulate:a", 1f, 0.75);
        var fade = new ColorRect { Name = "TitleFade", Color = Colors.Black,
            MouseFilter = MouseFilterEnum.Ignore };
        fade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(fade);
        var fadeIn = CreateTween();
        fadeIn.TweenProperty(fade, "color:a", 0f, 1.1);
        fadeIn.TweenCallback(Callable.From(fade.QueueFree));
    }

    public override void _Process(double delta)
    {
        if (_titleTip is null || !_titleTip.IsVisibleInTree()) return;
        _titleTipElapsed += delta;
        if (_titleTipElapsed < 7) return;
        _titleTipElapsed = 0;
        _titleTipIndex = (_titleTipIndex + 1) % TitleTips.Length;
        var tip = _titleTip;
        var tween = CreateTween();
        tween.TweenProperty(tip, "modulate:a", 0f, 0.3);
        tween.TweenCallback(Callable.From(() => tip.Text = TitleTips[_titleTipIndex]));
        tween.TweenProperty(tip, "modulate:a", 1f, 0.4);
    }
}
