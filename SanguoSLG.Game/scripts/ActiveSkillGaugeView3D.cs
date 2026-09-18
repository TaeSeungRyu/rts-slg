using Godot;
using SanguoSLG.Core.Simulation;

namespace SanguoSLG.Game;

/// <summary>부대 위에 표시하는 5일 액티브 충전 원. 월드 공간에서 항상 카메라를 향한다.</summary>
public sealed partial class ActiveSkillGaugeView3D : Node3D
{
    private static readonly Color Empty = new(0.16f, 0.16f, 0.18f, 0.88f);
    private static readonly Color Filled = new(1f, 0.77f, 0.22f, 1f);
    private readonly MeshInstance3D[] _segments = new MeshInstance3D[ActiveGauge.ReadyDays];

    public override void _Ready()
    {
        Position = new Vector3(0f, 1.08f, 0f);
        for (var i = 0; i < _segments.Length; i++)
        {
            var angle = Mathf.DegToRad(-140f + i * 70f);
            var segment = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.055f, Height = 0.11f, RadialSegments = 12, Rings = 6 },
                Position = new Vector3(Mathf.Cos(angle) * 0.28f, Mathf.Sin(angle) * 0.18f, 0f),
                MaterialOverride = Material(Empty),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(segment);
            _segments[i] = segment;
        }
    }

    public void SetGauge(ActiveGauge gauge)
    {
        var filled = System.Math.Clamp(gauge.ElapsedDays, 0, ActiveGauge.ReadyDays);
        for (var i = 0; i < _segments.Length; i++)
            _segments[i].MaterialOverride = Material(i < filled ? Filled : Empty);
    }

    private static StandardMaterial3D Material(Color color) => new()
    {
        AlbedoColor = color,
        EmissionEnabled = true,
        Emission = color,
        EmissionEnergyMultiplier = color == Filled ? 1.4f : 0.25f,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        NoDepthTest = true,
        BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
    };
}
