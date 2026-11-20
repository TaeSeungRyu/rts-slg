using Godot;

namespace SanguoSLG.Game;

public sealed partial class NavalComposeModal : DeployComposeModal
{

    private const int LeftContentRightMargin = 12;

    public NavalComposeModal(Vector2 viewport, Texture2D background, Font font, string title)
        : base(viewport, background, font, title)
    {
        Name = "NavalComposeModal";



        var padding = new MarginContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        padding.AddThemeConstantOverride("margin_right", LeftContentRightMargin);
        LeftScroll.RemoveChild(LeftContent);
        LeftScroll.AddChild(padding);
        padding.AddChild(LeftContent);


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
