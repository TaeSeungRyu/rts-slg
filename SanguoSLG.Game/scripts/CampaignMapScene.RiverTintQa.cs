using Godot;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private void RunRiverTintQa()
    {
        var river = GD.Load<PackedScene>("res://assets/models/river-rocky.glb").Instantiate<Node3D>();
        MapView3D.TintRiverTile(river);
        var meshes = river.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().ToList();
        var water = meshes.Single(mesh => mesh.Name.ToString() == "water");
        var rocks = meshes.Where(mesh => mesh.Name.ToString().StartsWith("river-rock-", System.StringComparison.Ordinal)).ToList();
        var waterColor = ((StandardMaterial3D)water.MaterialOverride).AlbedoColor;
        var shallow = GD.Load<PackedScene>("res://assets/models/water.glb").Instantiate<Node3D>();
        MapView3D.TintWaterTile(shallow, TerrainType.WaterShallow);
        var shallowColor = ((StandardMaterial3D)shallow.FindChildren("*", "MeshInstance3D", true, false)
            .OfType<MeshInstance3D>().First().MaterialOverride).AlbedoColor;
        var passed = waterColor.R > shallowColor.R && waterColor.G > shallowColor.G
            && waterColor.B > shallowColor.B && rocks.Count == 3
            && rocks.All(rock => rock.MaterialOverride is StandardMaterial3D material
                && material.AlbedoColor.R < 0.5f && material.Roughness >= 0.9f)
            && rocks.Select(rock => ((StandardMaterial3D)rock.MaterialOverride).AlbedoColor).Distinct().Count() == 3;
        GD.Print($"[river-tint-qa] passed={passed} river={waterColor} shallow={shallowColor} rocks={rocks.Count}");
        river.QueueFree();
        shallow.QueueFree();
        GetTree().Quit(passed ? 0 : 1);
    }
}
