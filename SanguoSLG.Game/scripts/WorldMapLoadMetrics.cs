using System.Diagnostics;
using Godot;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

internal static class WorldMapLoadMetrics
{
    private static readonly Stopwatch Total = new();

    public static bool Enabled { get; private set; }
    public static double DataPreparationMs { get; private set; }
    public static double TerrainBuildMs { get; private set; }
    public static double MaterialPreparationMs { get; private set; }
    public static double ImportedMeshTuningMs { get; private set; }
    public static double WorldNodeBuildMs { get; private set; }
    public static double FirstFramesMs { get; private set; }
    public static int MaterialSurfaceCount { get; private set; }
    public static int ForestTileCount { get; private set; }
    public static int MountainTileCount { get; private set; }
    public static int FeatureMountainCount { get; private set; }

    public static void Begin(bool enabled)
    {
        Enabled = enabled;
        if (!enabled) return;
        DataPreparationMs = TerrainBuildMs = MaterialPreparationMs = ImportedMeshTuningMs = 0d;
        WorldNodeBuildMs = FirstFramesMs = 0d;
        MaterialSurfaceCount = ForestTileCount = MountainTileCount = FeatureMountainCount = 0;
        Total.Restart();
    }

    public static long Timestamp() => Enabled ? Stopwatch.GetTimestamp() : 0L;
    public static double ElapsedMilliseconds(long start)
        => Enabled ? Stopwatch.GetElapsedTime(start).TotalMilliseconds : 0d;

    public static void RecordDataPreparation(long start) => DataPreparationMs += ElapsedMilliseconds(start);
    public static void RecordTerrainBuild(long start) => TerrainBuildMs += ElapsedMilliseconds(start);
    public static void RecordImportedMeshTuning(long start) => ImportedMeshTuningMs += ElapsedMilliseconds(start);
    public static void RecordWorldNodeBuild(long start) => WorldNodeBuildMs += ElapsedMilliseconds(start);
    public static void RecordFirstFrames(long start) => FirstFramesMs += ElapsedMilliseconds(start);

    public static void RecordMaterialPreparation(long start, int surfaces)
    {
        if (!Enabled) return;
        MaterialPreparationMs += ElapsedMilliseconds(start);
        MaterialSurfaceCount += surfaces;
    }

    public static void RecordTerrain(TerrainType terrain)
    {
        if (!Enabled) return;
        if (terrain == TerrainType.Forest) ForestTileCount++;
        else if (terrain == TerrainType.Mountain) MountainTileCount++;
    }

    public static void RecordFeatureMountain()
    {
        if (Enabled) FeatureMountainCount++;
    }

    public static void Print()
    {
        if (!Enabled) return;
        Total.Stop();
        GD.Print($"[map-load-profile] total_ms={Total.Elapsed.TotalMilliseconds:F2} "
            + $"data_ms={DataPreparationMs:F2} terrain_mesh_ms={TerrainBuildMs:F2} "
            + $"material_ms={MaterialPreparationMs:F2} tune_ms={ImportedMeshTuningMs:F2} "
            + $"world_nodes_ms={WorldNodeBuildMs:F2} first_frames_ms={FirstFramesMs:F2} "
            + $"material_surfaces={MaterialSurfaceCount} forest_tiles={ForestTileCount} "
            + $"mountain_tiles={MountainTileCount} feature_mountains={FeatureMountainCount}");
    }
}
