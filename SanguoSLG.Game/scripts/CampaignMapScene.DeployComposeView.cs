using Godot;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    /// <summary>
    /// 전투편성 전용 고정 프레임. 일반 DeployScaffold의 콘텐츠 기반 크기 계산을 사용하지 않아
    /// 배경 PNG의 상단·본문·하단 구획과 내부 UI가 서로 밀어내지 않는다.
    /// </summary>
    private VBoxContainer DeployComposeScaffold(float contentWidth, float contentHeight,
        out ScrollContainer scroll, out PanelContainer panel)
    {
        var layer = new CanvasLayer { Layer = 20 };
        AddChild(layer);
        _modalLayer = layer;

        var backdrop = new ColorRect
        {
            Name = "ModalBackdrop",
            Color = new Color(0, 0, 0, 0.68f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        backdrop.GuiInput += input =>
        {
            if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) CloseModal();
        };
        layer.AddChild(backdrop);

        var viewport = GetViewport().GetVisibleRect().Size;
        var frameHeight = Mathf.Min(viewport.Y - 24f, contentHeight + 28f);
        var frameWidth = Mathf.Min(viewport.X - 24f, frameHeight * 1.5f);
        panel = new PanelContainer
        {
            Name = "DeployComposeFrame",
            CustomMinimumSize = new Vector2(frameWidth, frameHeight),
            Size = new Vector2(frameWidth, frameHeight),
            Position = (viewport - new Vector2(frameWidth, frameHeight)) / 2f,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        panel.SetMeta("modal_root", true);
        panel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        layer.AddChild(panel);

        var background = new TextureRect
        {
            Name = "DeployComposeBackground",
            Texture = GD.Load<Texture2D>(DeployCommandAssetRoot + "deploy_command_panel.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        panel.AddChild(background);

        var safeArea = new MarginContainer { Name = "DeployComposeSafeArea" };
        safeArea.AddThemeConstantOverride("margin_left", 28);
        safeArea.AddThemeConstantOverride("margin_right", 28);
        safeArea.AddThemeConstantOverride("margin_top", 12);
        safeArea.AddThemeConstantOverride("margin_bottom", 30);
        panel.AddChild(safeArea);

        scroll = new ScrollContainer
        {
            Name = "DeployComposeOuterScroll",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        safeArea.AddChild(scroll);
        var body = new VBoxContainer
        {
            Name = "DeployComposeBody",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        body.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(body);
        return body;
    }
}
