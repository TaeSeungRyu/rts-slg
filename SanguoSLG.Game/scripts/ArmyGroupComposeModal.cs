using Godot;

namespace SanguoSLG.Game;

public sealed partial class ArmyGroupComposeModal : DeployComposeModal
{
    public ArmyGroupComposeModal(Vector2 viewport, Texture2D background, Font font, string title)
        : base(viewport, background, font, title)
    {
        Name = "ArmyGroupComposeModal";
    }
}
