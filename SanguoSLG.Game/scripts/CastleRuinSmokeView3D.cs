using Godot;

namespace SanguoSLG.Game;

/// <summary>성벽이 무너진 성에서 낮게 피어오르는 소규모 회색 연기.</summary>
public sealed partial class CastleRuinSmokeView3D : Node3D
{
    public int RequestedEmitterCount { get; set; } = 1;
    public int EmitterCount { get; private set; }

    public override void _Ready()
    {
        var count = RequestedEmitterCount switch { >= 9 => 9, >= 4 => 4, _ => 1 };
        var side = count == 9 ? 3 : count == 4 ? 2 : 1;
        var spacing = count == 9 ? 0.18f : count == 4 ? 0.22f : 0f;
        for (var index = 0; index < count; index++)
        {
            var x = side == 1 ? 0f : (index % side - (side - 1) / 2f) * spacing;
            var z = side == 1 ? 0f : (index / side - (side - 1) / 2f) * spacing;
            var radius = 0.030f + (index % 3) * 0.003f;
            AddPlume(new Vector3(x, 0.16f + (index % 2) * 0.025f, z), radius, index * 0.27f);
        }
    }

    private void AddPlume(Vector3 position, float radius, float phase)
    {
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(0.18f, 0.19f, 0.20f, 0.08f));
        gradient.AddPoint(0.20f, new Color(0.25f, 0.26f, 0.27f, 0.48f));
        gradient.AddPoint(0.72f, new Color(0.16f, 0.17f, 0.18f, 0.30f));
        gradient.SetColor(1, new Color(0.12f, 0.13f, 0.14f, 0f));
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
            Amount = 11,
            Lifetime = 3.6f,
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
            InitialVelocityMin = 0.065f,
            InitialVelocityMax = 0.11f,
            Gravity = new Vector3(0.006f, 0.010f, -0.003f),
            ScaleAmountMin = 0.55f,
            ScaleAmountMax = 1.35f,
            ColorRamp = gradient,
        };
        AddChild(particles);
        EmitterCount++;
    }
}
