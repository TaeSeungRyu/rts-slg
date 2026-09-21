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

        AddFragment("ArmorBreak_ChestLeft", new Vector3(-0.105f, 0f, 0f), new Vector3(0.20f, 0.34f, 0.055f), steel, new Vector3(-0.42f, 0.12f, 0.18f));
        AddFragment("ArmorBreak_ChestRight", new Vector3(0.105f, 0f, 0f), new Vector3(0.20f, 0.34f, 0.055f), steel, new Vector3(0.42f, 0.12f, 0.18f));
        AddFragment("ArmorBreak_ShoulderLeft", new Vector3(-0.29f, 0.08f, 0f), new Vector3(0.18f, 0.15f, 0.065f), trim, new Vector3(-0.50f, 0.24f, 0.12f), -0.24f);
        AddFragment("ArmorBreak_ShoulderRight", new Vector3(0.29f, 0.08f, 0f), new Vector3(0.18f, 0.15f, 0.065f), trim, new Vector3(0.50f, 0.24f, 0.12f), 0.24f);
        AddFragment("ArmorBreak_WaistLeft", new Vector3(-0.10f, -0.25f, 0f), new Vector3(0.18f, 0.14f, 0.05f), trim, new Vector3(-0.34f, -0.22f, 0.20f));
        AddFragment("ArmorBreak_WaistRight", new Vector3(0.10f, -0.25f, 0f), new Vector3(0.18f, 0.14f, 0.05f), trim, new Vector3(0.34f, -0.22f, 0.20f));

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
        flashTween.TweenInterval(0.30f);
        flashTween.TweenProperty(flash, "scale", Vector3.One, 0.07f);
        flashTween.TweenProperty(flash, "scale", new Vector3(0.02f, 1.15f, 0.02f), 0.16f);
        flashTween.TweenCallback(Callable.From(flash.QueueFree));

        var appeared = new Godot.Timer { OneShot = true, WaitTime = 0.23 };
        AddChild(appeared);
        appeared.Timeout += () => ArmorAppeared = true;
        appeared.Start();
        var broken = new Godot.Timer { OneShot = true, WaitTime = 0.90 };
        AddChild(broken);
        broken.Timeout += () => BreakCompleted = true;
        broken.Start();
        var cleanup = new Godot.Timer { OneShot = true, WaitTime = 1.25 };
        AddChild(cleanup);
        cleanup.Timeout += QueueFree;
        cleanup.Start();
    }

    private void AddFragment(string name, Vector3 origin, Vector3 size, Material material, Vector3 scatter, float rotationZ = 0f)
    {
        var fragment = new MeshInstance3D
        {
            Name = name,
            Mesh = new BoxMesh { Size = size, Material = material },
            Position = origin,
            Rotation = new Vector3(0f, 0f, rotationZ),
            Scale = Vector3.One * 0.02f,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(fragment);
        _fragments.Add(fragment);
        MaxScatterDistance = Mathf.Max(MaxScatterDistance, scatter.Length());

        var tween = CreateTween();
        tween.TweenProperty(fragment, "scale", Vector3.One, 0.18f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenInterval(0.20f);
        tween.TweenProperty(fragment, "position", origin + scatter, 0.42f)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(fragment, "rotation", new Vector3(scatter.Y * 4f, scatter.X * 4f, rotationZ + scatter.X * 3f), 0.42f);
        tween.Parallel().TweenProperty(fragment, "scale", Vector3.One * 0.04f, 0.46f);
    }
}
