using Godot;

namespace SanguoSLG.Game;

/// <summary>응급치료 GLB를 재사용해 초록 십자가가 상승하는 진정 효과.</summary>
public sealed partial class CleanseCrossEffectView3D : Node3D
{
    private const string AssetPath = "res://assets/models/effect-field-medic.glb";
    private static readonly Vector3[] Offsets =
    [
        new(-0.30f, 0f, -0.08f), new(0.04f, 0f, 0.02f), new(0.30f, 0f, -0.04f),
        new(-0.15f, 0f, 0.11f), new(0.18f, 0f, 0.15f), new(-0.36f, 0f, 0.19f), new(0.38f, 0f, 0.23f),
    ];
    private Node3D? _anchor;
    public int CrossCount { get; private set; }
    public bool UsesGreenMaterial { get; private set; }

    public override void _Ready()
    {
        _anchor = GetParentOrNull<Node3D>();
        TopLevel = true;
        GlobalPosition = (_anchor?.GlobalPosition ?? GlobalPosition) + Vector3.Up * 0.22f;
        var packed = GD.Load<PackedScene>(AssetPath);
        if (packed is null) { QueueFree(); return; }
        var green = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.18f, 1f, 0.38f),
            EmissionEnabled = true,
            Emission = new Color(0.04f, 0.75f, 0.18f),
            EmissionEnergyMultiplier = 2.2f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        for (var i = 0; i < Offsets.Length; i++)
        {
            var cross = packed.Instantiate<Node3D>();
            AddChild(cross);
            foreach (var mesh in FindMeshes(cross)) mesh.MaterialOverride = green;
            cross.Position = Offsets[i] + Vector3.Down * (0.42f + i % 3 * 0.05f);
            cross.Scale = Vector3.One * 0.04f;
            var destination = Offsets[i] + Vector3.Up * (0.48f + i % 2 * 0.08f);
            var tween = CreateTween();
            tween.TweenInterval(i * 0.10f);
            tween.TweenProperty(cross, "scale", Vector3.One * 0.72f, 0.12f);
            tween.Parallel().TweenProperty(cross, "position", destination, 0.62f);
            tween.TweenProperty(cross, "position", destination + Vector3.Up * 0.16f, 0.18f);
            tween.Parallel().TweenProperty(cross, "scale", Vector3.One * 0.01f, 0.20f);
            CrossCount++;
        }
        UsesGreenMaterial = true;
        var cleanup = new Godot.Timer { OneShot = true, WaitTime = 1.90 };
        AddChild(cleanup); cleanup.Timeout += QueueFree; cleanup.Start();
    }

    public override void _Process(double delta)
    {
        if (IsInstanceValid(_anchor)) GlobalPosition = _anchor!.GlobalPosition + Vector3.Up * 0.22f;
        var camera = GetViewport()?.GetCamera3D();
        if (camera is not null) GlobalBasis = camera.GlobalBasis.Orthonormalized();
    }

    private static System.Collections.Generic.IEnumerable<MeshInstance3D> FindMeshes(Node node)
    {
        if (node is MeshInstance3D mesh) yield return mesh;
        foreach (Node child in node.GetChildren())
            foreach (var nested in FindMeshes(child)) yield return nested;
    }
}
