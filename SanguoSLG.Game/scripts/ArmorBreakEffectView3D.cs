using System.Collections.Generic;
using Godot;

namespace SanguoSLG.Game;

/// <summary>파갑 명중점 머리 위에 갑옷이 나타난 뒤 여러 조각으로 깨져 흩어지는 1회성 효과.</summary>
public sealed partial class ArmorBreakEffectView3D : Node3D
{
    private readonly List<MeshInstance3D> _fragments = [];

    public int FragmentCount => _fragments.Count;
    public bool ArmorAppeared { get; private set; }
    public bool BreakCompleted { get; private set; }
    public float MaxScatterDistance { get; private set; }
    public float ArmorHoldSeconds => 0.72f;

    public override void _Ready()
    {
        Position = new Vector3(0f, 0.67f, 0f);
        var steel = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.30f, 0.38f, 0.48f),
            Metallic = 0.9f,
            Roughness = 0.18f,
            EmissionEnabled = true,
            Emission = new Color(0.08f, 0.18f, 0.34f),
            EmissionEnergyMultiplier = 1.8f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        var trim = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.88f, 0.61f, 0.16f),
            Metallic = 0.85f,
            Roughness = 0.16f,
            EmissionEnabled = true,
            Emission = new Color(0.42f, 0.18f, 0.02f),
            EmissionEnergyMultiplier = 1.6f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };

        // 납작한 옷 실루엣이 아니라, 볼록한 좌우 흉갑과 큰 견갑·목가리개·금속 허리판으로
        // 한눈에 판금 갑옷임을 읽을 수 있게 한다. 각 부위가 그대로 파괴 파편이 된다.
        AddFragment("ArmorBreak_BreastplateLeft", new SphereMesh { Radius = 0.19f, Height = 0.38f, RadialSegments = 16, Rings = 8, Material = steel },
            new Vector3(-0.105f, 0.02f, 0f), new Vector3(0.86f, 1f, 0.28f), new Vector3(-0.19f, 0.07f, 0.10f));
        AddFragment("ArmorBreak_BreastplateRight", new SphereMesh { Radius = 0.19f, Height = 0.38f, RadialSegments = 16, Rings = 8, Material = steel },
            new Vector3(0.105f, 0.02f, 0f), new Vector3(0.86f, 1f, 0.28f), new Vector3(0.19f, 0.07f, 0.10f));
        AddFragment("ArmorBreak_PauldronLeft", new SphereMesh { Radius = 0.16f, Height = 0.22f, RadialSegments = 14, Rings = 7, Material = trim },
            new Vector3(-0.30f, 0.10f, 0f), new Vector3(1.20f, 0.68f, 0.34f), new Vector3(-0.24f, 0.13f, 0.08f), -0.20f);
        AddFragment("ArmorBreak_PauldronRight", new SphereMesh { Radius = 0.16f, Height = 0.22f, RadialSegments = 14, Rings = 7, Material = trim },
            new Vector3(0.30f, 0.10f, 0f), new Vector3(1.20f, 0.68f, 0.34f), new Vector3(0.24f, 0.13f, 0.08f), 0.20f);
        AddFragment("ArmorBreak_Gorget", new TorusMesh { InnerRadius = 0.075f, OuterRadius = 0.135f, Rings = 12, RingSegments = 8, Material = trim },
            new Vector3(0f, 0.27f, 0f), new Vector3(1.35f, 0.72f, 0.42f), new Vector3(0f, 0.20f, 0.08f));
        AddFragment("ArmorBreak_CenterRidge", new BoxMesh { Size = new Vector3(0.045f, 0.40f, 0.055f), Material = trim },
            new Vector3(0f, 0.01f, 0.045f), Vector3.One, new Vector3(0.04f, 0.16f, 0.13f));
        AddFragment("ArmorBreak_WaistLeft", new BoxMesh { Size = new Vector3(0.19f, 0.13f, 0.065f), Material = steel },
            new Vector3(-0.10f, -0.23f, 0f), Vector3.One, new Vector3(-0.16f, -0.15f, 0.11f), -0.10f);
        AddFragment("ArmorBreak_WaistRight", new BoxMesh { Size = new Vector3(0.19f, 0.13f, 0.065f), Material = steel },
            new Vector3(0.10f, -0.23f, 0f), Vector3.One, new Vector3(0.16f, -0.15f, 0.11f), 0.10f);

        var flash = new MeshInstance3D
        {
            Name = "ArmorBreak_CrackFlash",
            Mesh = new QuadMesh
            {
                Size = new Vector2(0.08f, 0.52f),
                Material = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    AlbedoColor = new Color(0.95f, 0.96f, 1f, 0.98f),
                    EmissionEnabled = true,
                    Emission = new Color(0.7f, 0.85f, 1f),
                    EmissionEnergyMultiplier = 4f,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                },
            },
            Position = new Vector3(0f, 0f, 0.045f),
            Rotation = new Vector3(0f, 0f, -0.18f),
            Scale = new Vector3(0.01f, 0.01f, 0.01f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(flash);
        var flashTween = CreateTween();
        flashTween.TweenInterval(1.08f);
        flashTween.TweenProperty(flash, "scale", Vector3.One, 0.10f);
        flashTween.TweenProperty(flash, "scale", new Vector3(0.02f, 1.15f, 0.02f), 0.24f);
        flashTween.TweenCallback(Callable.From(flash.QueueFree));

        var appeared = new Godot.Timer { OneShot = true, WaitTime = 0.32 };
        AddChild(appeared);
        appeared.Timeout += () => ArmorAppeared = true;
        appeared.Start();
        var broken = new Godot.Timer { OneShot = true, WaitTime = 1.92 };
        AddChild(broken);
        broken.Timeout += () => BreakCompleted = true;
        broken.Start();
        var cleanup = new Godot.Timer { OneShot = true, WaitTime = 2.45 };
        AddChild(cleanup);
        cleanup.Timeout += QueueFree;
        cleanup.Start();
    }

    private void AddFragment(string name, PrimitiveMesh mesh, Vector3 origin, Vector3 baseScale, Vector3 scatter, float rotationZ = 0f)
    {
        var fragment = new MeshInstance3D
        {
            Name = name,
            Mesh = mesh,
            Position = origin,
            Rotation = new Vector3(0f, 0f, rotationZ),
            Scale = baseScale * 0.02f,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(fragment);
        _fragments.Add(fragment);
        MaxScatterDistance = Mathf.Max(MaxScatterDistance, scatter.Length());

        var tween = CreateTween();
        tween.TweenProperty(fragment, "scale", baseScale, 0.28f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenInterval(ArmorHoldSeconds);
        tween.TweenProperty(fragment, "position", origin + scatter, 0.70f)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(fragment, "rotation", new Vector3(scatter.Y * 3f, scatter.X * 3f, rotationZ + scatter.X * 2f), 0.70f);
        tween.Parallel().TweenProperty(fragment, "scale", baseScale * 0.06f, 0.76f);
    }
}
