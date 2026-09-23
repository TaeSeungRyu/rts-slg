using Godot;

namespace SanguoSLG.Game;

/// <summary>Blender 청철 중갑이 시전자 부대를 감쌌다가 축소 소멸하는 철벽 효과.</summary>
public sealed partial class IronWallArmorEffectView3D : Node3D
{
    private const string AssetPath = "res://assets/models/effect-iron-wall.glb";
    private Node3D? _anchor;

    public bool LoadedFromGlb { get; private set; }
    public int ChestPlateCount { get; private set; }
    public bool HasProtectionRing { get; private set; }
    public bool ArmorAppeared { get; private set; }
    public bool DisplayCompleted { get; private set; }
    public float CameraFacingDot { get; private set; }
    public bool IsScreenAligned { get; private set; }

    public override void _Ready()
    {
        // 부대 자체의 방향·기울기를 상속하지 않는다. 철벽은 월드에서 똑바로 선 채
        // 카메라 쪽을 정면으로 바라보는 표식이다.
        _anchor = GetParentOrNull<Node3D>();
        var anchorPosition = _anchor?.GlobalPosition ?? GlobalPosition;
        TopLevel = true;
        GlobalPosition = anchorPosition + Vector3.Up * 0.68f;
        AlignToCamera();
        var scene = GD.Load<PackedScene>(AssetPath);
        if (scene is null)
        {
            GD.PushError($"철벽 GLB를 불러오지 못했습니다: {AssetPath}");
            QueueFree();
            return;
        }

        var visual = scene.Instantiate<Node3D>();
        visual.Name = "IronWallGlbVisual";
        AddChild(visual);
        LoadedFromGlb = true;
        ChestPlateCount = visual.FindChildren("IronWall_ChestPlate_*", "", true, false).Count;
        var ring = visual.FindChild("IronWall_ProtectionRing", true, false) as Node3D;
        HasProtectionRing = ring is not null;

        var baseScale = visual.Scale;
        visual.Scale = baseScale * 0.02f;
        var armorTween = CreateTween();
        armorTween.TweenProperty(visual, "scale", baseScale, 0.26f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        armorTween.TweenInterval(0.78f);
        armorTween.TweenProperty(visual, "scale", baseScale * 1.08f, 0.12f);
        armorTween.TweenProperty(visual, "scale", baseScale * 0.01f, 0.28f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);

        if (ring is not null)
        {
            var ringScale = ring.Scale;
            var ringTween = CreateTween().SetLoops(3);
            ringTween.TweenProperty(ring, "scale", ringScale * 1.12f, 0.18f);
            ringTween.TweenProperty(ring, "scale", ringScale * 0.94f, 0.18f);
        }

        var appeared = new Godot.Timer { OneShot = true, WaitTime = 0.30 };
        AddChild(appeared);
        appeared.Timeout += () => ArmorAppeared = true;
        appeared.Start();
        var completed = new Godot.Timer { OneShot = true, WaitTime = 1.48 };
        AddChild(completed);
        completed.Timeout += () => DisplayCompleted = true;
        completed.Start();
        var cleanup = new Godot.Timer { OneShot = true, WaitTime = 1.62 };
        AddChild(cleanup);
        cleanup.Timeout += QueueFree;
        cleanup.Start();
    }

    public override void _Process(double delta)
    {
        if (IsInstanceValid(_anchor)) GlobalPosition = _anchor!.GlobalPosition + Vector3.Up * 0.68f;
        AlignToCamera();
    }

    private void AlignToCamera()
    {
        var camera = GetViewport()?.GetCamera3D();
        if (camera is null) return;
        // 게임 카메라의 상하 기울기까지 그대로 사용한다. 월드 수직을 강제하면 탑다운 화면에서
        // 정면 면이 눕거나 얇은 측면처럼 보이므로, GLB 면을 화면 평면과 정확히 평행하게 둔다.
        var cameraBasis = camera.GlobalBasis.Orthonormalized();
        GlobalBasis = cameraBasis;
        CameraFacingDot = GlobalBasis.Z.Dot(cameraBasis.Z);
        IsScreenAligned = Mathf.Abs(GlobalBasis.X.Dot(cameraBasis.X)) > 0.999f
            && Mathf.Abs(GlobalBasis.Y.Dot(cameraBasis.Y)) > 0.999f
            && CameraFacingDot > 0.999f;
    }
}
