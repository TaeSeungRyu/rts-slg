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
    private readonly StandardMaterial3D _declaredShade = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        AlbedoColor = new Color(0.035f, 0.04f, 0.06f, 0.42f),
        DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
    };
    private readonly Dictionary<ulong, (GeometryInstance3D Mesh, HexCoord Tile, Material? Original)> _meshes = new();
    private readonly Dictionary<HexCoord, List<ulong>> _meshesByTile = new();
    private readonly HashSet<HexCoord> _lastVisible = new();
    private readonly HashSet<HexCoord> _lastDeclared = new();
    private bool _hasApplied;
    public long MaterialWriteCount { get; private set; }

    public void Register(Node root, HexCoord tile)
    {
        if (root.IsQueuedForDeletion()) return;
        if (root is MeshInstance3D or MultiMeshInstance3D)
        {
            var mesh = (GeometryInstance3D)root;
            var id = mesh.GetInstanceId();
            if (_meshes.TryAdd(id, (mesh, tile, mesh.MaterialOverlay)))
            {
                if (!_meshesByTile.TryGetValue(tile, out var entries))
                    _meshesByTile[tile] = entries = new List<ulong>();
                entries.Add(id);
                if (_hasApplied) UpdateMesh(id);
            }
        }
        foreach (var child in root.GetChildren()) Register(child, tile);
    }

    public void RegisterMap()
    {
        foreach (var child in map.GetChildren().OfType<Node3D>())
            Register(child, map.WorldToHex(child.GlobalPosition));
    }

    public void Apply(IReadOnlySet<HexCoord> visible, IReadOnlySet<HexCoord>? declaredCities = null)
    {
        if (!_hasApplied)
        {
            _lastVisible.UnionWith(visible);
            if (declaredCities is not null) _lastDeclared.UnionWith(declaredCities);
            _hasApplied = true;
            foreach (var id in _meshes.Keys.ToArray()) UpdateMesh(id);
            return;
        }

        var changed = new HashSet<HexCoord>(_lastVisible);
        changed.SymmetricExceptWith(visible);
        var declaredChanged = new HashSet<HexCoord>(_lastDeclared);
        declaredChanged.SymmetricExceptWith(declaredCities ?? Enumerable.Empty<HexCoord>());
        changed.UnionWith(declaredChanged);
        _lastVisible.Clear();
        _lastVisible.UnionWith(visible);
        _lastDeclared.Clear();
        if (declaredCities is not null) _lastDeclared.UnionWith(declaredCities);
        foreach (var tile in changed)
            if (_meshesByTile.TryGetValue(tile, out var entries))
                foreach (var id in entries) UpdateMesh(id);
    }

    private void UpdateMesh(ulong id)
    {
        if (!_meshes.TryGetValue(id, out var entry)) return;
        if (!GodotObject.IsInstanceValid(entry.Mesh) || entry.Mesh.IsQueuedForDeletion())
        {
            _meshes.Remove(id);
            return;
        }
        var material = _lastVisible.Contains(entry.Tile) ? entry.Original
            : _lastDeclared.Contains(entry.Tile) ? _declaredShade : _shade;
        if (entry.Mesh.MaterialOverlay != material)
        {
            entry.Mesh.MaterialOverlay = material;
            MaterialWriteCount++;
        }
    }
}
