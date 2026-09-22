using System.Collections.Generic;
using Godot;

namespace SanguoSLG.Game;

/// <summary>작은 육각 방패 여러 장이 위에서 내려와 아군 진형을 덮는 귀갑진 GLB 효과.</summary>
public sealed partial class TurtleFormationEffectView3D : Node3D
{
    private const string AssetPath = "res://assets/models/effect-turtle-formation.glb";
    private static readonly Vector3[] FormationOffsets =
    [
        new(-0.42f, 0.18f, 0f), new(0f, 0.28f, 0f), new(0.42f, 0.18f, 0f),
        new(-0.25f, -0.08f, 0f), new(0.25f, -0.08f, 0f),
        new(-0.13f, -0.34f, 0f), new(0.13f, -0.34f, 0f),
    ];

    private Node3D? _anchor;
    private readonly List<Node3D> _shields = [];

    public bool LoadedFromGlb { get; private set; }
    public int ShieldCount => _shields.Count;
    public int LandedCount { get; private set; }
    public bool DisplayCompleted { get; private set; }
    public float CameraFacingDot { get; private set; }

    public override void _Ready()
    {
        _anchor = GetParentOrNull<Node3D>();
        var anchorPosition = _anchor?.GlobalPosition ?? GlobalPosition;
        TopLevel = true;
        GlobalPosition = anchorPosition + Vector3.Up * 0.58f;
        AlignToCamera();

        var packed = GD.Load<PackedScene>(AssetPath);
        if (packed is null)
        {
            GD.PushError($"귀갑진 GLB를 불러오지 못했습니다: {AssetPath}");
            QueueFree();
            return;
        }
        LoadedFromGlb = true;

        for (var index = 0; index < FormationOffsets.Length; index++)
        {
            var shield = packed.Instantiate<Node3D>();
            shield.Name = $"TurtleFallingShield_{index + 1}";
            AddChild(shield);
            _shields.Add(shield);
            var destination = FormationOffsets[index];
            var baseScale = shield.Scale * 0.72f;
            shield.Position = destination + Vector3.Up * (0.72f + index % 3 * 0.12f);
            shield.Scale = baseScale * 0.02f;

            var delay = index * 0.075f;
            var tween = CreateTween();
            tween.TweenInterval(delay);
            tween.TweenProperty(shield, "scale", baseScale, 0.12f)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tween.Parallel().TweenProperty(shield, "position", destination, 0.28f)
                .SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
            tween.TweenCallback(Callable.From(() => LandedCount++));
            tween.TweenInterval(0.58f);
            tween.TweenProperty(shield, "scale", baseScale * 0.01f, 0.22f);
        }

        var completed = new Godot.Timer { OneShot = true, WaitTime = 1.08 };
        AddChild(completed);
        completed.Timeout += () => DisplayCompleted = true;
        completed.Start();
        var cleanup = new Godot.Timer { OneShot = true, WaitTime = 1.48 };
        AddChild(cleanup);
        cleanup.Timeout += QueueFree;
        cleanup.Start();
    }

    public override void _Process(double delta)
    {
        if (IsInstanceValid(_anchor)) GlobalPosition = _anchor!.GlobalPosition + Vector3.Up * 0.58f;
        AlignToCamera();
    }

    private void AlignToCamera()
    {
        var camera = GetViewport()?.GetCamera3D();
        if (camera is null) return;
        var front = camera.GlobalPosition - GlobalPosition;
        front.Y = 0f;
        if (front.LengthSquared() < 0.000001f) return;
        front = front.Normalized();
        GlobalBasis = new Basis(Vector3.Up.Cross(front).Normalized(), Vector3.Up, front).Orthonormalized();
        CameraFacingDot = GlobalBasis.Z.Dot(front);
    }
}
