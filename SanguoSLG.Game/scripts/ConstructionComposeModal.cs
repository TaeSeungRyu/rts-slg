using Godot;

namespace SanguoSLG.Game;

public partial class ConstructionComposeModal : SupplyComposeModal
{
    public ConstructionComposeModal(Vector2 viewport, Texture2D background, Font font, string title)
        : base(viewport, background, font, title)
    {
        Name = "ConstructionComposeModal";
        LeftContent.Name = "ConstructionComposeTroopAndCargo";
        RightContent.Name = "ConstructionComposeOfficer";
    }
}
