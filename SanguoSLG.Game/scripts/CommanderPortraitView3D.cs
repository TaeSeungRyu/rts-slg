using Godot;

namespace SanguoSLG.Game;

/// <summary>선택한 부대의 주장 원형 초상을 스킬 충전원보다 위에 표시한다.</summary>
public sealed partial class CommanderPortraitView3D : Node3D
{
    public const float TroopLabelHeightOffset = 1.10f;
    public const float LabelPixelSize = 0.005f;
    public const float PortraitPixelSize = 0.00165f * PortraitScaleRatio;
    private const float EstimatedLabelHalfHeight = 0.14f;
    private const float PortraitHalfHeight = 192f * PortraitPixelSize * 0.5f;
    public const float HeightOffset = TroopLabelHeightOffset + EstimatedLabelHalfHeight + 5f * LabelPixelSize + PortraitHalfHeight;
    public const float SkillGaugeHeight = 1.08f;
    public const float PortraitScaleRatio = 0.8f;
    private Sprite3D _portrait = null!;

    public bool HasPortrait => _portrait is not null && _portrait.Texture is not null;
    public bool IsAboveSkillGauge => HeightOffset > SkillGaugeHeight;
    public bool HasThinGoldBorder { get; private set; }
    public bool HasMipmappedBorder => _portrait?.Texture?.GetImage().HasMipmaps() == true;
    public static float EstimatedGapPixels
        => (HeightOffset - PortraitHalfHeight - TroopLabelHeightOffset - EstimatedLabelHalfHeight) / LabelPixelSize;

    public override void _Ready()
    {
        TopLevel = true;
        _portrait = new Sprite3D
        {
            PixelSize = PortraitPixelSize,
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
        _portrait.Texture = AddThinGoldRing(texture);
        HasThinGoldBorder = true;
    }

    private static ImageTexture AddThinGoldRing(Texture2D texture)
    {
        var image = texture.GetImage().Duplicate() as Image;
        if (image is null || image.IsEmpty()) return ImageTexture.CreateFromImage(texture.GetImage());
        var width = image.GetWidth();
        var height = image.GetHeight();
        var center = new Vector2((width - 1) * 0.5f, (height - 1) * 0.5f);
        var radius = System.Math.Min(width, height) * 0.485f;
        var thickness = System.Math.Max(1.0f, System.Math.Min(width, height) * 0.012f);
        const float feather = 0.85f;
        var gold = new Color(0.93f, 0.72f, 0.30f, 0.96f);
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var distance = new Vector2(x, y).DistanceTo(center);
            var outerCoverage = Mathf.Clamp((float)(radius + feather - distance), 0f, 1f);
            var innerCoverage = Mathf.Clamp((float)(distance - (radius - thickness - feather)), 0f, 1f);
            var ringCoverage = Mathf.Min(outerCoverage, innerCoverage);
            if (ringCoverage > 0f)
            {
                var source = image.GetPixel(x, y);
                gold.A = Mathf.Max(source.A, ringCoverage * 0.96f);
                image.SetPixel(x, y, source.Lerp(gold, ringCoverage));
            }
            if (distance > radius + feather)
            {
                var outside = image.GetPixel(x, y);
                outside.A = 0f;
                image.SetPixel(x, y, outside);
            }
        }
        image.GenerateMipmaps();
        return ImageTexture.CreateFromImage(image);
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
