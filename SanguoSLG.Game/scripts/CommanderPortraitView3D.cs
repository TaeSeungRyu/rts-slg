using Godot;

namespace SanguoSLG.Game;

/// <summary>선택한 부대의 주장 원형 초상을 스킬 충전원보다 위에 표시한다.</summary>
public sealed partial class CommanderPortraitView3D : Node3D
{
    public const float HeightOffset = 1.48f;
    public const float SkillGaugeHeight = 1.08f;
    private Sprite3D _portrait = null!;

    public bool HasPortrait => _portrait is not null && _portrait.Texture is not null;
    public bool IsAboveSkillGauge => HeightOffset > SkillGaugeHeight;

    public override void _Ready()
    {
        TopLevel = true;
        _portrait = new Sprite3D
        {
            PixelSize = 0.00165f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            Modulate = Colors.White,
            RenderPriority = 5,
        };
        AddChild(_portrait);
        AlignAboveParent();
    }

    public override void _Process(double delta) => AlignAboveParent();

    public void SetPortrait(Texture2D texture)
    {
        if (_portrait is null) return;
        _portrait.Texture = texture;
    }

    private void AlignAboveParent()
    {
        if (GetParent() is not Node3D owner) return;
        GlobalPosition = owner.GlobalPosition + Vector3.Up * HeightOffset;
        var camera = GetViewport()?.GetCamera3D();
        if (camera is not null && !GlobalPosition.IsEqualApprox(camera.GlobalPosition))
            LookAt(camera.GlobalPosition, Vector3.Up);
    }
}
