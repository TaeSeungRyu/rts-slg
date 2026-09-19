using System.Linq;
using Godot;

namespace SanguoSLG.Game;

/// <summary>무쌍 명중점에서 한 번 터지는 Blender 붉은 구름 효과.</summary>
public sealed partial class PeerlessCloudEffectView3D : Node3D
{
    public int SpawnedBurstCount { get; private set; }

    public override void _Ready()
    {
        // 지면 아래로 묻히지 않되 병사 머리 위까지 뜨지 않는 편대 중심 높이.
        Position = new Vector3(0f, 0.24f, 0f);
        Scale = Vector3.One * 0.92f;
        var visual = GD.Load<PackedScene>("res://assets/models/effect-peerless-red-cloud.glb").Instantiate<Node3D>();
        AddChild(visual);
        foreach (var player in FindPlayers(visual))
        {
            var animation = player.GetAnimationList().FirstOrDefault(x => x != "RESET");
            if (animation is not null) player.Play(animation);
        }

        // GLB 애니메이션과 별도로 불투명한 붉은 연무를 시간차 방출한다. 저사양/먼 카메라에서도
        // 한 덩어리 구름이 아니라 편대 안쪽에서 여러 번 '펑펑' 터지는 형태가 확실히 보인다.
        SpawnPuff(new Vector3(-0.18f, 0.02f, -0.10f));
        SchedulePuff(0.14, new Vector3(0.16f, 0.04f, 0.12f));
        SchedulePuff(0.28, new Vector3(-0.05f, 0.06f, 0.16f));
        SchedulePuff(0.42, new Vector3(0.11f, 0.03f, -0.15f));

        var cleanup = new Godot.Timer { OneShot = true, WaitTime = 1.55 };
        AddChild(cleanup);
        cleanup.Timeout += QueueFree;
        cleanup.Start();
    }

    private void SchedulePuff(double delay, Vector3 position)
    {
        var timer = new Godot.Timer { OneShot = true, WaitTime = delay };
        AddChild(timer);
        timer.Timeout += () =>
        {
            SpawnPuff(position);
            timer.QueueFree();
        };
        timer.Start();
    }

    private void SpawnPuff(Vector3 position)
    {
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(1f, 0.12f, 0.06f, 0.95f));
        gradient.AddPoint(0.55f, new Color(0.72f, 0.02f, 0.04f, 0.72f));
        gradient.SetColor(1, new Color(0.28f, 0.01f, 0.02f, 0f));
        var material = new StandardMaterial3D
        {
            AlbedoColor = Colors.White,
            EmissionEnabled = true,
            Emission = new Color(0.95f, 0.04f, 0.03f),
            EmissionEnergyMultiplier = 2.2f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        var particles = new CpuParticles3D
        {
            Position = position,
            Amount = 16,
            Lifetime = 0.48f,
            OneShot = true,
            Explosiveness = 1f,
            Emitting = true,
            Mesh = new SphereMesh { Radius = 0.045f, Height = 0.09f, Material = material },
            Direction = Vector3.Up,
            Spread = 180f,
            InitialVelocityMin = 0.10f,
            InitialVelocityMax = 0.26f,
            Gravity = new Vector3(0f, -0.08f, 0f),
            ScaleAmountMin = 0.65f,
            ScaleAmountMax = 1.4f,
            ColorRamp = gradient,
        };
        AddChild(particles);
        SpawnedBurstCount++;
    }

    private static System.Collections.Generic.IEnumerable<AnimationPlayer> FindPlayers(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is AnimationPlayer player) yield return player;
            foreach (var nested in FindPlayers(child)) yield return nested;
        }
    }
}
