using Godot;
using SanguoSLG.Core.Simulation;
using System.Linq;

namespace SanguoSLG.Game;

/// <summary>부대 위에 표시하는 5일 액티브 충전 원. 월드 공간에서 항상 카메라를 향한다.</summary>
public sealed partial class ActiveSkillGaugeView3D : Node3D
{
    public int FilledSegments { get; private set; }
    public string? SkillCode { get; private set; }
    private static readonly Color Empty = new(0.16f, 0.16f, 0.18f, 0.88f);
    private static readonly Color Filled = new(1f, 0.77f, 0.22f, 1f);
    private readonly MeshInstance3D[] _segments = new MeshInstance3D[ActiveGauge.ReadyDays];
    private static readonly Vector3[] SegmentPositions =
    {
        new(-0.10f, 0.19f, 0f), new(0.10f, 0.19f, 0f),
        new(-0.20f, -0.16f, 0f), new(0f, -0.20f, 0f), new(0.20f, -0.16f, 0f),
    };
    private Sprite3D _icon = null!;
    private Label3D _progress = null!;

    public override void _Ready()
    {
        Position = new Vector3(0f, 1.08f, 0f);
        _icon = new Sprite3D
        {
            PixelSize = 0.00062f,
            Position = Vector3.Zero,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            Modulate = new Color(1f, 1f, 1f, 0.94f),
        };
        AddChild(_icon);
        _progress = new Label3D
        {
            Text = "0/5",
            FontSize = 32,
            PixelSize = 0.002f,
            OutlineSize = 10,
            Position = new Vector3(0f, -0.27f, 0f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = new Color(1f, 0.88f, 0.54f),
        };
        AddChild(_progress);
        for (var i = 0; i < _segments.Length; i++)
        {
            var segment = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.055f, Height = 0.11f, RadialSegments = 12, Rings = 6 },
                Position = SegmentPositions[i],
                MaterialOverride = Material(Empty),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(segment);
            _segments[i] = segment;
        }
    }

    public bool HasTwoOverThreeLayout
        => _segments.Count(x => x.Position.Y > 0f) == 2 && _segments.Count(x => x.Position.Y < 0f) == 3;

    public void SetGauge(ActiveGauge gauge)
    {
        var filled = System.Math.Clamp(gauge.ElapsedDays, 0, ActiveGauge.ReadyDays);
        FilledSegments = filled;
        for (var i = 0; i < _segments.Length; i++)
            _segments[i].MaterialOverride = Material(i < filled ? Filled : Empty);
        _progress.Text = gauge.IsReady ? "발동!" : $"{filled}/{ActiveGauge.ReadyDays}";
    }

    public void SetSkill(ActiveSkill? skill, ActiveGauge gauge)
    {
        SkillCode = skill?.Code;
        _icon.Texture = ActiveSkillIcons.Load(skill?.Code);
        _icon.Visible = _icon.Texture is not null;
        SetGauge(gauge);
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
