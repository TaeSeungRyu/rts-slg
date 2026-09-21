using Godot;

namespace SanguoSLG.Game;

/// <summary>연환격 명중점에서 큰 칼 두 자루가 좌우로 교차하며 편대를 연속으로 베는 1회성 효과.</summary>
public sealed partial class ChainStrikeSwordEffectView3D : Node3D
{
    public int SlashCount { get; private set; }
    public int CompletedSlashCount { get; private set; }
    public float SlashSpan { get; private set; }

    public override void _Ready()
    {
        Position = new Vector3(0f, 0.34f, 0f);
        SpawnSlash(fromLeft: true, delay: 0f);
        SpawnSlash(fromLeft: false, delay: 0.18f);

        var cleanup = new Godot.Timer { OneShot = true, WaitTime = 1.12 };
        AddChild(cleanup);
        cleanup.Timeout += QueueFree;
        cleanup.Start();
    }

    private void SpawnSlash(bool fromLeft, double delay)
    {
        const float edge = 0.68f;
        var direction = fromLeft ? 1f : -1f;
        var sword = BuildSword(fromLeft);
        sword.Position = new Vector3(-edge * direction, 0.05f, 0.08f);
        sword.Rotation = new Vector3(0.10f, 0f, fromLeft ? -0.76f : 0.76f);
        sword.Scale = Vector3.One * 0.18f;
        AddChild(sword);
        SlashCount++;
        SlashSpan = edge * 2f;

        var tween = CreateTween();
        tween.TweenInterval(delay);
        tween.TweenProperty(sword, "scale", Vector3.One, 0.09f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sword, "position", new Vector3(edge * direction, -0.05f, 0.08f), 0.30f)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.InOut);
        tween.Parallel().TweenProperty(sword, "rotation:z", fromLeft ? 0.76f : -0.76f, 0.30f);
        tween.TweenProperty(sword, "scale", Vector3.One * 0.05f, 0.20f)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tween.Finished += () =>
        {
            CompletedSlashCount++;
            sword.QueueFree();
        };
    }

    private static Node3D BuildSword(bool fromLeft)
    {
        var root = new Node3D { Name = fromLeft ? "ChainStrike_LeftSlash" : "ChainStrike_RightSlash" };
        var steel = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.84f, 0.91f, 1f, 0.96f),
            Metallic = 0.88f,
            Roughness = 0.12f,
            EmissionEnabled = true,
            Emission = new Color(0.30f, 0.52f, 0.92f),
            EmissionEnergyMultiplier = 2.2f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        var gold = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.72f, 0.46f, 0.10f),
            Metallic = 0.78f,
            Roughness = 0.22f,
            EmissionEnabled = true,
            Emission = new Color(0.34f, 0.15f, 0.02f),
            EmissionEnergyMultiplier = 1.3f,
        };

        root.AddChild(new MeshInstance3D
        {
            Name = "ChainStrike_Blade",
            Mesh = new BoxMesh { Size = new Vector3(0.12f, 0.78f, 0.035f), Material = steel },
            Position = new Vector3(0f, 0.12f, 0f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        root.AddChild(new MeshInstance3D
        {
            Name = "ChainStrike_Tip",
            Mesh = new PrismMesh { Size = new Vector3(0.12f, 0.20f, 0.035f), Material = steel },
            Position = new Vector3(0f, 0.61f, 0f),
            Rotation = new Vector3(0f, 0f, Mathf.Pi),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        root.AddChild(new MeshInstance3D
        {
            Name = "ChainStrike_Guard",
            Mesh = new BoxMesh { Size = new Vector3(0.34f, 0.07f, 0.07f), Material = gold },
            Position = new Vector3(0f, -0.31f, 0f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        root.AddChild(new MeshInstance3D
        {
            Name = "ChainStrike_Handle",
            Mesh = new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.04f, Height = 0.30f, Material = gold },
            Position = new Vector3(0f, -0.48f, 0f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        return root;
    }
}
