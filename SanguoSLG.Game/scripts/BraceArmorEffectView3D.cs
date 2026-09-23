using System.Linq;
using Godot;

namespace SanguoSLG.Game;

/// <summary>철벽의 조선 두정갑 GLB를 더 작고 밝게 표시하는 하위 방어 액티브 방비 효과.</summary>
public sealed partial class BraceArmorEffectView3D : Node3D
{
    private const string AssetPath = "res://assets/models/effect-iron-wall.glb";
    private Node3D? _anchor;

    public bool LoadedFromIronWallGlb { get; private set; }
    public int ChestPlateCount { get; private set; }
    public int LightenedSurfaceCount { get; private set; }
    public bool DisplayCompleted { get; private set; }
    public bool IsScreenAligned { get; private set; }

    public override void _Ready()
    {
        _anchor = GetParentOrNull<Node3D>();
        var anchorPosition = _anchor?.GlobalPosition ?? GlobalPosition;
        TopLevel = true;
        GlobalPosition = anchorPosition + Vector3.Up * 0.52f;
        AlignToCamera();

        var packed = GD.Load<PackedScene>(AssetPath);
        if (packed is null)
        {
            GD.PushError($"방비용 철벽 GLB를 불러오지 못했습니다: {AssetPath}");
            QueueFree();
            return;
        }
        var visual = packed.Instantiate<Node3D>();
        visual.Name = "BraceIronWallGlbVisual";
        AddChild(visual);
        LoadedFromIronWallGlb = true;
        ChestPlateCount = visual.FindChildren("IronWall_ChestPlate_*", "", true, false).Count;
        LightenMaterials(visual);

        // 철벽보다 28% 작게 표시한다.
        var baseScale = visual.Scale * 0.72f;
        visual.Scale = baseScale * 0.02f;
        var tween = CreateTween();
        tween.TweenProperty(visual, "scale", baseScale, 0.22f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenInterval(0.62f);
        tween.TweenProperty(visual, "scale", baseScale * 0.01f, 0.25f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);

        var completed = new Godot.Timer { OneShot = true, WaitTime = 0.88 };
        AddChild(completed);
        completed.Timeout += () => DisplayCompleted = true;
        completed.Start();
        var cleanup = new Godot.Timer { OneShot = true, WaitTime = 1.16 };
        AddChild(cleanup);
        cleanup.Timeout += QueueFree;
        cleanup.Start();
    }

    public override void _Process(double delta)
    {
        if (IsInstanceValid(_anchor)) GlobalPosition = _anchor!.GlobalPosition + Vector3.Up * 0.52f;
        AlignToCamera();
    }

    private void LightenMaterials(Node3D visual)
    {
        foreach (var meshInstance in visual.FindChildren("*", "", true, false).OfType<MeshInstance3D>())
        {
            if (meshInstance.Mesh is null) continue;
            for (var surface = 0; surface < meshInstance.Mesh.GetSurfaceCount(); surface++)
            {
                var source = meshInstance.GetActiveMaterial(surface) as StandardMaterial3D;
                if (source is null) continue;
                var light = (StandardMaterial3D)source.Duplicate();
                light.AlbedoColor = light.AlbedoColor.Lightened(0.42f);
                light.Emission = light.Emission.Lightened(0.30f);
                light.EmissionEnergyMultiplier *= 0.72f;
                meshInstance.SetSurfaceOverrideMaterial(surface, light);
                LightenedSurfaceCount++;
            }
        }
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
