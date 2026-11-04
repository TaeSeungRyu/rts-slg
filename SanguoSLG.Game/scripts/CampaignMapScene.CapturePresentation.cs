using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

namespace SanguoSLG.Game;

public partial class CampaignMapScene
{
    private readonly Queue<(string Name, bool Victory)> _capturePresentationQueue = new();
    private bool _capturePresentationPlaying;

    private void QueueCityCapturePresentation(City city, CaptureReport report)
    {
        if (report.NewOwner != Player && report.OldOwner != Player) return;
        _capturePresentationQueue.Enqueue((city.Name, report.NewOwner == Player));
        if (!_capturePresentationPlaying) PlayNextCapturePresentation();
    }

    private void PlayNextCapturePresentation()
    {
        if (_capturePresentationQueue.Count == 0 || !IsInsideTree())
        {
            _capturePresentationPlaying = false;
            return;
        }
        _capturePresentationPlaying = true;
        var (name, victory) = _capturePresentationQueue.Dequeue();
        var layer = new CanvasLayer { Name = "CityCapturePresentation", Layer = 90 };
        AddChild(layer);
        var overlay = new ColorRect { Color = new Color(0.02f, 0.015f, 0.02f, 0.72f),
            MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0) };
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(overlay);
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(center);
        var card = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(560, 0), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        var frame = new StyleBoxFlat { BgColor = new Color("17120f"),
            BorderColor = victory ? new Color("dfb45f") : new Color("a78b91") };
        frame.SetBorderWidthAll(2);
        frame.SetCornerRadiusAll(10);
        frame.SetContentMarginAll(12);
        card.AddThemeStyleboxOverride("panel", frame);
        center.AddChild(card);
        var content = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        content.AddThemeConstantOverride("separation", 8);
        card.AddChild(content);
        var portrait = new TextureRect { Texture = GD.Load<Texture2D>(victory
                ? "res://assets/ui/capture/win.png" : "res://assets/ui/capture/lose.png"),
            CustomMinimumSize = new Vector2(540, 360),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = Control.MouseFilterEnum.Ignore };
        content.AddChild(portrait);
        var title = new Label { Text = victory ? $"{name} 점령" : $"{name} 함락",
            HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1, 1, 1, 0) };
        title.AddThemeFontSizeOverride("font_size", 34);
        title.AddThemeColorOverride("font_color", victory ? new Color("f3d38e") : new Color("edb9b6"));
        title.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.95f));
        title.AddThemeConstantOverride("shadow_offset_x", 2);
        title.AddThemeConstantOverride("shadow_offset_y", 3);
        content.AddChild(title);
        var tween = CreateTween();
        tween.TweenProperty(overlay, "modulate:a", 1f, 0.24f);
        tween.Parallel().TweenProperty(card, "scale", Vector2.One, 0.32f)
            .From(new Vector2(0.93f, 0.93f)).SetTrans(Tween.TransitionType.Back);
        tween.TweenProperty(title, "modulate:a", 1f, 0.26f);
        tween.TweenInterval(1.45f);
        tween.TweenProperty(overlay, "modulate:a", 0f, 0.3f);
        tween.Finished += () =>
        {
            if (GodotObject.IsInstanceValid(layer)) layer.QueueFree();
            PlayNextCapturePresentation();
        };
    }
}
