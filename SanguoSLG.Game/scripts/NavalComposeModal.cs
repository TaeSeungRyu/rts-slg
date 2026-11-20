using Godot;

namespace SanguoSLG.Game;

public sealed partial class NavalComposeModal : DeployComposeModal
{
    public NavalComposeModal(Vector2 viewport, Texture2D background, Font font, string title)
        : base(viewport, background, font, title)
    {
        Name = "NavalComposeModal";
    }
}
