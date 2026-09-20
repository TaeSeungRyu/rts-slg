using Godot;

namespace SanguoSLG.Game;

/// <summary>돌파 중 공격 부대의 몸을 감싸며 뒤로 흘러가는 1회성 연기.</summary>
public sealed partial class BreakthroughSmokeEffectView3D : Node3D
{
    public int SmokeEmitterCount { get; private set; }

    public override void _Ready()
    {
        Position = new Vector3(0f, 0.20f, 0f);
        AddSmoke(new Vector3(-0.16f, 0f, -0.08f), 0.055f);
        AddSmoke(new Vector3(0f, 0.03f, 0.05f), 0.070f);
        AddSmoke(new Vector3(0.16f, 0f, -0.08f), 0.055f);

        var cleanup = new Godot.Timer { OneShot = true, WaitTime = 1.05 };
        AddChild(cleanup);
        cleanup.Timeout += QueueFree;
        cleanup.Start();
    }

    private void AddSmoke(Vector3 position, float radius)
    {
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(0.72f, 0.70f, 0.66f, 0.72f));
        gradient.AddPoint(0.45f, new Color(0.34f, 0.32f, 0.30f, 0.52f));
        gradient.SetColor(1, new Color(0.12f, 0.11f, 0.10f, 0f));
        var material = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        AddChild(new CpuParticles3D
        {
            Position = position,
            Amount = 13,
            Lifetime = 0.52f,
            OneShot = true,
            Explosiveness = 0.35f,
            Emitting = true,
            Mesh = new SphereMesh { Radius = radius, Height = radius * 2f, RadialSegments = 7, Rings = 4, Material = material },
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 0.10f,
            Direction = Vector3.Back,
            Spread = 35f,
            InitialVelocityMin = 0.16f,
            InitialVelocityMax = 0.34f,
            Gravity = new Vector3(0f, 0.08f, 0f),
            ScaleAmountMin = 0.55f,
            ScaleAmountMax = 1.65f,
            ColorRamp = gradient,
        });
        SmokeEmitterCount++;
    }
}
