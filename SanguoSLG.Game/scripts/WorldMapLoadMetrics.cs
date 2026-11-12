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
    public static double LoadReadyMs { get; private set; }
    public static double FrameSampleMs { get; private set; }
    public static int FrameSampleCount { get; private set; }
    public static int MaterialSurfaceCount { get; private set; }
    public static int MaterialCreatedCount { get; private set; }
    public static int ForestTileCount { get; private set; }
    public static int MountainTileCount { get; private set; }
    public static int FeatureMountainCount { get; private set; }

    public static void Begin(bool enabled)
    {
        Enabled = enabled;
        if (!enabled) return;
        DataPreparationMs = TerrainBuildMs = MaterialPreparationMs = ImportedMeshTuningMs = 0d;
        WorldNodeBuildMs = FirstFramesMs = LoadReadyMs = FrameSampleMs = 0d;
        FrameSampleCount = 0;
        MaterialSurfaceCount = MaterialCreatedCount = ForestTileCount = MountainTileCount = FeatureMountainCount = 0;
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
    public static void MarkLoadReady() => LoadReadyMs = Total.Elapsed.TotalMilliseconds;

    public static void RecordFrameSample(long start, int frames)
    {
        FrameSampleMs = ElapsedMilliseconds(start);
        FrameSampleCount = frames;
    }

    public static void RecordMaterialPreparation(long start, int surfaces, int created)
    {
        if (!Enabled) return;
        MaterialPreparationMs += ElapsedMilliseconds(start);
        MaterialSurfaceCount += surfaces;
        MaterialCreatedCount += created;
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
        var fps = FrameSampleMs > 0d ? FrameSampleCount * 1000d / FrameSampleMs : 0d;
        GD.Print($"[map-load-profile] total_ms={LoadReadyMs:F2} "
            + $"data_ms={DataPreparationMs:F2} terrain_mesh_ms={TerrainBuildMs:F2} "
            + $"material_ms={MaterialPreparationMs:F2} tune_ms={ImportedMeshTuningMs:F2} "
            + $"world_nodes_ms={WorldNodeBuildMs:F2} first_frames_ms={FirstFramesMs:F2} "
            + $"sample_frames={FrameSampleCount} sample_ms={FrameSampleMs:F2} fps={fps:F2} "
            + $"material_surfaces={MaterialSurfaceCount} material_created={MaterialCreatedCount} "
            + $"forest_tiles={ForestTileCount} "
            + $"mountain_tiles={MountainTileCount} feature_mountains={FeatureMountainCount}");
    }
}
