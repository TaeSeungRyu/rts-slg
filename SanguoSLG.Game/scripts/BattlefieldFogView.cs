using System.Collections.Generic;
using System.Linq;
using Godot;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

public sealed class BattlefieldFogView(MapView3D map)
{
    private readonly StandardMaterial3D _shade = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        AlbedoColor = new Color(0.02f, 0.025f, 0.045f, 0.72f),
        DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
    };
    private readonly Dictionary<ulong, (GeometryInstance3D Mesh, HexCoord Tile, Material? Original)> _meshes = new();

    public void Register(Node root, HexCoord tile)
    {
        if (root.IsQueuedForDeletion()) return;
        if (root is MeshInstance3D or MultiMeshInstance3D)
        {
            var mesh = (GeometryInstance3D)root;
            _meshes.TryAdd(mesh.GetInstanceId(), (mesh, tile, mesh.MaterialOverlay));
        }
        foreach (var child in root.GetChildren()) Register(child, tile);
    }

    public void RegisterMap()
    {
        foreach (var child in map.GetChildren().OfType<Node3D>())
            Register(child, map.WorldToHex(child.GlobalPosition));
    }

    public void Apply(IReadOnlySet<HexCoord> visible)
    {
        foreach (var (id, entry) in _meshes.ToArray())
        {
            if (!GodotObject.IsInstanceValid(entry.Mesh) || entry.Mesh.IsQueuedForDeletion())
            {
                _meshes.Remove(id);
                continue;
            }
            entry.Mesh.MaterialOverlay = visible.Contains(entry.Tile) ? entry.Original : _shade;
        }
    }
}
