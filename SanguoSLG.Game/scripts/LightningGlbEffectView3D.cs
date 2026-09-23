using Godot;

namespace SanguoSLG.Game;

/// <summary>기존 effect-lightning.glb 애니메이션을 대상 위치에서 한 번 재생하는 낙뢰 효과.</summary>
public sealed partial class LightningGlbEffectView3D : Node3D
{
    private const string AssetPath = "res://assets/models/effect-lightning.glb";
    public bool LoadedFromGlb { get; private set; }
    public bool AnimationStarted { get; private set; }
    public bool AnimationCompleted { get; private set; }
    public int WhiteZigzagCount { get; private set; }
    public int RuntimeSegmentCount { get; private set; }
    private Node3D? _anchor;
    private Node3D? _billboard;

    public override void _Ready()
    {
        _anchor = GetParentOrNull<Node3D>();
        var anchorPosition = _anchor?.GlobalPosition ?? GlobalPosition;
        TopLevel = true;
        GlobalPosition = anchorPosition + Vector3.Up * 0.44f;
        var packed = GD.Load<PackedScene>(AssetPath);
        if (packed is null) { GD.PushError($"낙뢰 GLB를 불러오지 못했습니다: {AssetPath}"); QueueFree(); return; }
        var visual = packed.Instantiate<Node3D>();
        visual.Name = "LightningGlbVisual";
        AddChild(visual);
        visual.Visible = false; // GLB는 형상 원본. 실기기 표시는 카메라 정면 런타임 메시에 맡긴다.
        WhiteZigzagCount = visual.FindChildren("white_zigzag_*", "", true, false).Count;
        LoadedFromGlb = true;
        _billboard = new Node3D { Name = "LightningVisibleBillboard" };
        AddChild(_billboard);
        AlignToCamera();
        var white = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(0.88f, 1f, 1f, 0.64f), EmissionEnabled = true,
            Emission = new Color(0.46f, 1f, 0.94f, 0.68f), EmissionEnergyMultiplier = 6.2f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        var paths = new[]
        {
            new[] { new Vector2(-.30f,.72f), new(-.36f,.53f), new(-.25f,.39f), new(-.34f,.23f), new(-.23f,.08f), new(-.30f,-.15f) },
            new[] { new Vector2(.00f,.82f), new(.08f,.60f), new(-.05f,.44f), new(.07f,.27f), new(-.04f,.09f), new(.02f,-.18f) },
            new[] { new Vector2(.30f,.74f), new(.22f,.56f), new(.35f,.40f), new(.24f,.25f), new(.34f,.06f), new(.27f,-.16f) },
        };
        for (var boltIndex = 0; boltIndex < paths.Length; boltIndex++)
            BuildRuntimeBolt(paths[boltIndex], boltIndex * 0.060, white);
        AnimationStarted = RuntimeSegmentCount == 15;
        if (!AnimationStarted) { GD.PushError("낙뢰 GLB에서 flash_1~3 노드를 찾지 못했습니다."); QueueFree(); return; }
        var completed = new Godot.Timer { OneShot = true, WaitTime = 0.45 };
        AddChild(completed);
        completed.Timeout += () => AnimationCompleted = true;
        completed.Start();
        var cleanup = new Godot.Timer { OneShot = true, WaitTime = 0.82 };
        AddChild(cleanup);
        cleanup.Timeout += QueueFree;
        cleanup.Start();
    }

    public override void _Process(double delta)
    {
        if (IsInstanceValid(_anchor)) GlobalPosition = _anchor!.GlobalPosition + Vector3.Up * 0.44f;
        AlignToCamera();
    }

    private void BuildRuntimeBolt(Vector2[] points, double delay, Material material)
    {
        if (_billboard is null) return;
        var segments = new System.Collections.Generic.List<MeshInstance3D>();
        for (var i = 0; i < points.Length - 1; i++)
        {
            var delta = points[i + 1] - points[i];
            var segment = new MeshInstance3D
            {
                Name = $"WhiteLightningSegment_{RuntimeSegmentCount + 1}",
                Mesh = new BoxMesh { Size = new Vector3(delta.Length(), 0.045f - i * 0.004f, 0.018f), Material = material },
                Position = new Vector3((points[i].X + points[i + 1].X) * 0.5f, (points[i].Y + points[i + 1].Y) * 0.5f, 0f),
                Rotation = new Vector3(0f, 0f, Mathf.Atan2(delta.Y, delta.X)),
                Visible = false,
            };
            _billboard.AddChild(segment);
            segments.Add(segment);
            RuntimeSegmentCount++;
        }
        var tween = CreateTween();
        tween.TweenInterval(delay);
        foreach (var segment in segments)
        {
            tween.TweenCallback(Callable.From(() => segment.Visible = true));
            tween.TweenInterval(0.014f);
        }
        tween.TweenInterval(0.12f);
        tween.TweenCallback(Callable.From(() => segments.ForEach(segment => segment.Visible = false)));
    }

    private void AlignToCamera()
    {
        var camera = GetViewport()?.GetCamera3D();
        if (camera is not null && IsInstanceValid(_billboard))
            _billboard!.GlobalBasis = camera.GlobalBasis.Orthonormalized();
    }
}
