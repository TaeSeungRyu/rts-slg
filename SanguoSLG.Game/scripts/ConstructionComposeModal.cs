using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// 건설부대 편성 전용 프레임. 배경 PNG 경로는 CampaignMapScene.ConstructionUi.cs에서
/// DeployCommandAssetRoot + "deploy_command_panel.png"로 전달한다.
/// 전체 크기, 프레임 안전 여백, 제목줄·본문·하단 구획의 위치는 부모
/// SupplyComposeModal.cs의 생성자에서 조절한다. 여기서는 건설 전용 노드 이름만 지정한다.
/// </summary>
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
