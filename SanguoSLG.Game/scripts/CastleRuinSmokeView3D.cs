using Godot;

namespace SanguoSLG.Game;

/// <summary>성벽이 무너진 성에서 낮게 피어오르는 소규모 회색 연기.</summary>
public sealed partial class CastleRuinSmokeView3D : Node3D
{
    public int EmitterCount { get; private set; }

    public override void _Ready()
    {
        AddPlume(new Vector3(-0.20f, 0.20f, 0.03f), 0.032f, 0.00f);
        AddPlume(new Vector3(0.04f, 0.16f, -0.12f), 0.038f, 0.55f);
        AddPlume(new Vector3(0.22f, 0.18f, 0.10f), 0.030f, 1.05f);
    }

    private void AddPlume(Vector3 position, float radius, float phase)
    {
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(0.40f, 0.41f, 0.42f, 0.06f));
        gradient.AddPoint(0.20f, new Color(0.48f, 0.49f, 0.50f, 0.42f));
        gradient.AddPoint(0.72f, new Color(0.34f, 0.35f, 0.36f, 0.25f));
        gradient.SetColor(1, new Color(0.30f, 0.31f, 0.32f, 0f));
        var material = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        var particles = new CpuParticles3D
        {
            Name = $"RuinSmoke{EmitterCount + 1}",
            Position = position,
            Amount = 9,
            Lifetime = 2.1f,
            Preprocess = phase,
            Randomness = 0.45f,
            Emitting = true,
            LocalCoords = true,
            Mesh = new SphereMesh
            {
                Radius = radius,
                Height = radius * 1.7f,
                RadialSegments = 7,
                Rings = 4,
                Material = material,
            },
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 0.035f,
            Direction = Vector3.Up,
            Spread = 13f,
            InitialVelocityMin = 0.13f,
            InitialVelocityMax = 0.22f,
            Gravity = new Vector3(0.015f, 0.025f, -0.008f),
            ScaleAmountMin = 0.55f,
            ScaleAmountMax = 1.35f,
            ColorRamp = gradient,
        };
        AddChild(particles);
        EmitterCount++;
    }
}
