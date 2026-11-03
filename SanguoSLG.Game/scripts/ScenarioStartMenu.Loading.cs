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
        var overlay = new ColorRect { Color = new Color("101a1a"), MouseFilter = MouseFilterEnum.Stop,
            Modulate = new Color(1, 1, 1, 0) };
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        layer.AddChild(overlay);
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        overlay.AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(440, 225) };
        var frame = new StyleBoxFlat { BgColor = new Color("202b28"), BorderColor = new Color("d1ae65") };
        frame.SetBorderWidthAll(2);
        frame.SetCornerRadiusAll(10);
        frame.SetContentMarginAll(20);
        panel.AddThemeStyleboxOverride("panel", frame);
        center.AddChild(panel);
        var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        column.AddThemeConstantOverride("separation", 12);
        panel.AddChild(column);
        var emblem = new MenuLoadingEmblem { CustomMinimumSize = new Vector2(92, 92),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        column.AddChild(emblem);
        var title = new Label { Text = "전장을 준비하고 있습니다", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 22);
        title.AddThemeColorOverride("font_color", new Color("e5c985"));
        column.AddChild(title);
        column.AddChild(new Label { Text = "지도와 장수, 세력의 기록을 불러오는 중입니다...",
            HorizontalAlignment = HorizontalAlignment.Center });
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
