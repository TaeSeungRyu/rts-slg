using Godot;

namespace SanguoSLG.Game;

/// <summary>작은 붉은 십자가들이 아군 진형 아래에서 위로 시간차 상승하는 응급치료 GLB 효과.</summary>
public sealed partial class FieldMedicCrossEffectView3D : Node3D
{
    private const string AssetPath = "res://assets/models/effect-field-medic.glb";
    private static readonly Vector3[] CrossOffsets =
    [
        new(-0.30f, 0f, -0.08f), new(0.04f, 0f, 0.02f), new(0.30f, 0f, -0.04f),
        new(-0.15f, 0f, 0.11f), new(0.18f, 0f, 0.15f), new(-0.36f, 0f, 0.19f), new(0.38f, 0f, 0.23f),
    ];

    private Node3D? _anchor;

    public bool LoadedFromGlb { get; private set; }
    public int CrossCount { get; private set; }
    public int CompletedCrossCount { get; private set; }
    public bool IsScreenAligned { get; private set; }
    public bool DisplayCompleted { get; private set; }

    public override void _Ready()
    {
        _anchor = GetParentOrNull<Node3D>();
        var anchorPosition = _anchor?.GlobalPosition ?? GlobalPosition;
        TopLevel = true;
        GlobalPosition = anchorPosition + Vector3.Up * 0.22f;
        AlignToCamera();

        var packed = GD.Load<PackedScene>(AssetPath);
        if (packed is null)
        {
            GD.PushError($"응급치료 GLB를 불러오지 못했습니다: {AssetPath}");
            QueueFree();
            return;
        }

        for (var index = 0; index < CrossOffsets.Length; index++)
        {
            var cross = packed.Instantiate<Node3D>();
            cross.Name = $"FieldMedicCross_{index + 1}";
            AddChild(cross);
            cross.Position = CrossOffsets[index] + Vector3.Down * (0.42f + index % 3 * 0.05f);
            cross.Scale = Vector3.One * 0.04f;
            var destination = CrossOffsets[index] + Vector3.Up * (0.48f + index % 2 * 0.08f);
            var tween = CreateTween();
            tween.TweenInterval(index * 0.10f);
            tween.TweenProperty(cross, "scale", Vector3.One * 0.72f, 0.12f)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tween.Parallel().TweenProperty(cross, "position", destination, 0.62f)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(cross, "position", destination + Vector3.Up * 0.16f, 0.18f);
            tween.Parallel().TweenProperty(cross, "scale", Vector3.One * 0.01f, 0.20f);
            tween.TweenCallback(Callable.From(() => CompletedCrossCount++));
            CrossCount++;
        }
        LoadedFromGlb = true;

        var completed = new Godot.Timer { OneShot = true, WaitTime = 1.62 };
        AddChild(completed);
        completed.Timeout += () => DisplayCompleted = true;
        completed.Start();
        var cleanup = new Godot.Timer { OneShot = true, WaitTime = 1.90 };
        AddChild(cleanup);
        cleanup.Timeout += QueueFree;
        cleanup.Start();
    }

    public override void _Process(double delta)
    {
        if (IsInstanceValid(_anchor)) GlobalPosition = _anchor!.GlobalPosition + Vector3.Up * 0.22f;
        AlignToCamera();
    }

    private void AlignToCamera()
    {
        var camera = GetViewport()?.GetCamera3D();
        if (camera is null) return;
        var cameraBasis = camera.GlobalBasis.Orthonormalized();
        GlobalBasis = cameraBasis;
        IsScreenAligned = Mathf.Abs(GlobalBasis.X.Dot(cameraBasis.X)) > 0.999f
            && Mathf.Abs(GlobalBasis.Y.Dot(cameraBasis.Y)) > 0.999f
            && Mathf.Abs(GlobalBasis.Z.Dot(cameraBasis.Z)) > 0.999f;
    }
}
