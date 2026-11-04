using Godot;

namespace SanguoSLG.Game;

public sealed partial class ScenarioStartMenu
{
    private bool _campaignLoading;
    private static CanvasLayer? _loadingLayer;
    public static bool IsLoadingVisible => _loadingLayer is not null && GodotObject.IsInstanceValid(_loadingLayer);
    public static bool IsLoadingAnimationActive => IsLoadingVisible
        && HasLoadingEmblem(_loadingLayer!);
    public static int LoadingAnimationFrames => IsLoadingVisible
        ? FindLoadingEmblem(_loadingLayer!)?.FrameCount ?? 0 : 0;

    private static bool HasLoadingEmblem(Node node)
        => node is MenuLoadingEmblem || node.GetChildren().Any(HasLoadingEmblem);

    private static MenuLoadingEmblem? FindLoadingEmblem(Node node)
    {
        if (node is MenuLoadingEmblem emblem) return emblem;
        foreach (var child in node.GetChildren())
            if (FindLoadingEmblem(child) is { } found) return found;
        return null;
    }

    private async void ChangeToCampaign()
    {
        if (_campaignLoading) return;
        _campaignLoading = true;
        _loadingLayer?.QueueFree();
        var layer = new CanvasLayer { Layer = 100 };
        _loadingLayer = layer;
        GetTree().Root.AddChild(layer);
        var overlay = new ColorRect { Color = Colors.White, MouseFilter = MouseFilterEnum.Stop,
            Modulate = new Color(1, 1, 1, 0) };
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        layer.AddChild(overlay);
        var backdrop = new TextureRect { Texture = GD.Load<Texture2D>("res://assets/ui/loading/loading_bg.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        overlay.AddChild(backdrop);
        var shade = new ColorRect { Color = new Color(0.025f, 0.02f, 0.025f, 0.22f),
            MouseFilter = MouseFilterEnum.Ignore };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        overlay.AddChild(shade);
        var bottom = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        bottom.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
        bottom.OffsetTop = -190;
        var frame = new StyleBoxFlat { BgColor = new Color(0.055f, 0.045f, 0.045f, 0.94f),
            BorderColor = new Color("d1ae65") };
        frame.BorderWidthTop = 2;
        frame.SetContentMarginAll(20);
        bottom.AddThemeStyleboxOverride("panel", frame);
        overlay.AddChild(bottom);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 20);
        bottom.AddChild(row);
        var emblem = new MenuLoadingEmblem { CustomMinimumSize = new Vector2(100, 100),
            SizeFlagsVertical = SizeFlags.ShrinkCenter };
        row.AddChild(emblem);
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(460, 0),
            SizeFlagsVertical = SizeFlags.ShrinkCenter };
        column.AddThemeConstantOverride("separation", 10);
        row.AddChild(column);
        var title = new Label { Text = "전장을 준비하고 있습니다" };
        title.AddThemeFontSizeOverride("font_size", 23);
        title.AddThemeColorOverride("font_color", new Color("e5c985"));
        column.AddChild(title);
        var bar = new ProgressBar { Indeterminate = true, ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 18) };
        column.AddChild(bar);
        var tips = new[] { "지휘 능력이 높으면 더 많은 병력을 편성할 수 있습니다.",
            "공성전에서는 보급과 군량을 확인하세요.",
            "궁병은 원거리 공격에 강하지만 가까운 적을 경계해야 합니다.",
            "기병은 평지에서 빠르게 이동합니다." };
        var tip = new Label { Text = tips[GD.RandRange(0, tips.Length - 1)] };
        tip.AddThemeFontSizeOverride("font_size", 15);
        column.AddChild(tip);
        CreateTween().TweenProperty(overlay, "modulate:a", 1f, 0.2f);
        await ToSignal(GetTree().CreateTimer(0.23), SceneTreeTimer.SignalName.Timeout);
        if (IsInsideTree()) GetTree().ReloadCurrentScene();
    }

    public static void FinishLoading(SceneTree tree)
    {
        if (_loadingLayer is null) return;
        tree.CreateTimer(0.1).Timeout += () =>
        {
            if (_loadingLayer is not { } layer || !GodotObject.IsInstanceValid(layer)) return;
            var tween = layer.CreateTween();
            tween.TweenProperty(layer.GetChild<ColorRect>(0), "modulate:a", 0f, 0.22f);
            tween.Finished += () => { layer.QueueFree(); if (ReferenceEquals(_loadingLayer, layer)) _loadingLayer = null; };
        };
    }

    public static void AbortLoading()
    {
        _loadingLayer?.QueueFree();
        _loadingLayer = null;
    }
}
