using Godot;

namespace SanguoSLG.Game;

public sealed partial class NavalComposeModal : DeployComposeModal
{

    private const int LeftContentRightMargin = 4;
    public VBoxContainer PaddedLeftContent { get; }    

    public NavalComposeModal(Vector2 viewport, Texture2D background, Font font, string title)
        : base(viewport, background, font, title)
    {
        Name = "NavalComposeModal";



        var padding = new MarginContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        padding.AddThemeConstantOverride("margin_right", LeftContentRightMargin);
        LeftContent.AddChild(padding);

        PaddedLeftContent = new VBoxContainer();
        PaddedLeftContent.AddThemeConstantOverride(
            "separation", LeftContent.GetThemeConstant("separation"));
        padding.AddChild(PaddedLeftContent);


        if (LeftScroll.GetParent() is PanelContainer leftFrame)
        {
            leftFrame.CustomMinimumSize = new Vector2(0, 615);
            leftFrame.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        }

        if (RightContent.GetParent() is PanelContainer rightFrame)
        {
            rightFrame.CustomMinimumSize = new Vector2(0, 615);
            rightFrame.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        }      

    }
}
