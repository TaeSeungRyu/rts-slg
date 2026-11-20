using Godot;

namespace SanguoSLG.Game;

public sealed partial class TransportComposeModal : SupplyComposeModal
{
    public TransportComposeModal(Vector2 viewport, Texture2D background, Font font, string title)
        : base(viewport, background, font, title)
    {
        Name = "TransportComposeModal";
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
