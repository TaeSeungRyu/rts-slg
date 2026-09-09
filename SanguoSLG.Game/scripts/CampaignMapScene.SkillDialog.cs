namespace SanguoSLG.Game;

using Godot;

public sealed partial class CampaignMapScene
{
    private void ShowSkillDescription(string name, string category, string description)
    {
        _confirmLayer?.QueueFree();
        var layer = new CanvasLayer { Layer = 42 };
        AddChild(layer);
        _confirmLayer = layer;
        void Close()
        {
            layer.QueueFree();
            if (_confirmLayer == layer) { _confirmLayer = null; }
        }

        var backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.6f) };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        backdrop.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true }) { Close(); }
        };
        layer.AddChild(backdrop);
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(center);
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        panel.AddThemeStyleboxOverride("panel", Frame(Ink, Gold, 2, 10, 14));
        center.AddChild(panel);

        var viewport = GetViewport().GetVisibleRect().Size;
        var width = Mathf.Min(440f, viewport.X - 64f);
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(width, 0) };
        box.AddThemeConstantOverride("separation", 12);
        panel.AddChild(box);
        var titleRow = new HBoxContainer();
        box.AddChild(titleRow);
        var title = MakeLabel(name, 20, GoldBright);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        titleRow.AddChild(title);
        var close = MakeButton("✕");
        close.Pressed += Close;
        titleRow.AddChild(close);
        box.AddChild(MakeLabel(category, 13, Gold));
        box.AddChild(GoldRule());
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        box.AddChild(scroll);
        var text = MakeLabel(description, 15, Parchment);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        text.CustomMinimumSize = new Vector2(width - 20f, 0);
        text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(text);
        void Fit() => scroll.CustomMinimumSize = new Vector2(width,
            Mathf.Min(text.GetCombinedMinimumSize().Y, Mathf.Max(80f, viewport.Y - 240f)));
        text.MinimumSizeChanged += Fit;
        Fit();
        var done = MakeButton("닫기", accent: true);
        done.CustomMinimumSize = new Vector2(0, 36);
        done.Pressed += Close;
        box.AddChild(done);
    }
}
