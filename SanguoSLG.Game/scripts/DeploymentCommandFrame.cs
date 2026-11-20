using Godot;

namespace SanguoSLG.Game;

public sealed partial class DeploymentCommandFrame : PanelContainer
{
    public HBoxContainer Header { get; }
    public VBoxContainer List { get; }
    public VBoxContainer Details { get; }
    public HBoxContainer Actions { get; }
    public Button NewButton { get; }
    public Button CloseButton { get; }
    public VBoxContainer Root { get; }

    public DeploymentCommandFrame(Vector2 viewport, Texture2D background, Font font,
        string title, string countText, string listTitle, string detailTitle)
    {
        Name = "DeploymentCommandFrame";
        var height = Mathf.Min(viewport.Y - 28f, 970f);
        var width = Mathf.Min(viewport.X - 28f, height * 1.5f);
        CustomMinimumSize = Size = new Vector2(width, height);
        Position = (viewport - Size) / 2f;
        MouseFilter = MouseFilterEnum.Stop;
        SetMeta("modal_root", true);
        AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        var image = new TextureRect
        {
            Name = "DeploymentCommandBackground",
            Texture = background,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        image.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(image);
        var safe = new MarginContainer { Name = "DeploymentCommandSafeArea" };
        safe.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        safe.AddThemeConstantOverride("margin_left", 45);
        safe.AddThemeConstantOverride("margin_right", 45);
        safe.AddThemeConstantOverride("margin_top", 13);
        safe.AddThemeConstantOverride("margin_bottom", 24);
        AddChild(safe);
        Root = new VBoxContainer { Name = "DeploymentCommandContent" };
        Root.AddThemeConstantOverride("separation", 8);
        safe.AddChild(Root);

        Header = new HBoxContainer { CustomMinimumSize = new Vector2(0, 76) };
        Header.AddThemeConstantOverride("separation", 10);
        Header.AddChild(new Control { CustomMinimumSize = new Vector2(28, 0), MouseFilter = MouseFilterEnum.Ignore });
        var heading = new Label
        {
            Text = title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        heading.AddThemeFontOverride("font", font);
        heading.AddThemeFontSizeOverride("font_size", 27);
        heading.AddThemeColorOverride("font_color", new Color("f4d777"));
        Header.AddChild(heading);
        CloseButton = new Button { Name = "DeploymentCommandClose", Text = "×", CustomMinimumSize = new Vector2(30, 30) };
        CloseButton.AddThemeFontOverride("font", font);
        Header.AddChild(CloseButton);
        Root.AddChild(Header);

        var bodyMargin = new MarginContainer
        {
            Name = "DeploymentCommandBodyMargin",
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        bodyMargin.AddThemeConstantOverride("margin_left", 5);
        bodyMargin.AddThemeConstantOverride("margin_top", 5);
        bodyMargin.AddThemeConstantOverride("margin_right", 5);
        bodyMargin.AddThemeConstantOverride("margin_bottom", 5);
        Root.AddChild(bodyMargin);

        var body = new VBoxContainer
        {
            Name = "DeploymentCommandBody",
            Alignment = BoxContainer.AlignmentMode.Begin,
        };
        body.AddThemeConstantOverride("separation", 8);
        bodyMargin.AddChild(body);

        var status = new HBoxContainer { CustomMinimumSize = new Vector2(0, 46) };
        var count = new Label { Text = countText, VerticalAlignment = VerticalAlignment.Center };
        count.AddThemeFontOverride("font", font);
        count.AddThemeFontSizeOverride("font_size", 17);
        count.AddThemeColorOverride("font_color", new Color("f4d777"));
        status.AddChild(count);
        status.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        NewButton = new Button { Name = "DeploymentCommandNew", Text = "＋ 신규 편성", CustomMinimumSize = new Vector2(170, 42) };
        NewButton.AddThemeFontOverride("font", font);

        var newButtonMargin = new MarginContainer();
        newButtonMargin.AddThemeConstantOverride("margin_top", 15);
        newButtonMargin.AddChild(NewButton);
        status.AddChild(newButtonMargin);        

        body.AddChild(status);


        var columns = new HBoxContainer
        {
            Name = "DeploymentCommandColumns",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        columns.AddThemeConstantOverride("separation", 12);
        body.AddChild(columns);
        var left = new VBoxContainer { CustomMinimumSize = new Vector2(405, 0), SizeFlagsVertical = SizeFlags.ExpandFill };
        left.AddThemeConstantOverride("separation", 7);
        left.AddChild(SectionTitle(listTitle, font));
        columns.AddChild(left);
        var scroll = new ScrollContainer
        {
            Name = "DeploymentCommandListScroll",
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        GameUiSkin.ApplyQuickListScroll(scroll);
        left.AddChild(scroll);
        List = new VBoxContainer { Name = "DeploymentCommandList", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        List.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(List);
        Details = new VBoxContainer
        {
            Name = "DeploymentCommandDetails",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        Details.AddThemeConstantOverride("separation", 7);
        Details.AddChild(SectionTitle(detailTitle, font));
        columns.AddChild(Details);
        Actions = new HBoxContainer { Name = "DeploymentCommandActions", CustomMinimumSize = new Vector2(0, 46) };
        Actions.AddThemeConstantOverride("separation", 8);
        Root.AddChild(Actions);
    }

    private static Label SectionTitle(string text, Font font)
    {
        var label = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontOverride("font", font);
        label.AddThemeFontSizeOverride("font_size", 17);
        label.AddThemeColorOverride("font_color", new Color("f4d777"));
        return label;
    }

    public int BodyColumnCount()
        => GetNode<HBoxContainer>( "DeploymentCommandSafeArea/DeploymentCommandContent/" +
        "DeploymentCommandBodyMargin/DeploymentCommandBody/DeploymentCommandColumns").GetChildCount();
}
