using Godot;
using System.Linq;

namespace SanguoSLG.Game;

public partial class FieldCloudFade3D : Node
{
    private MeshInstance3D[] _clouds = [];
    private float[] _phaseOffsets = [];
    private float[] _cycleSpeeds = [];
    private double _elapsed;

    public int ConfiguredCloudCount => _clouds.Length;
    public bool HasVariedTimings => _phaseOffsets.Distinct().Count() > 1 && _cycleSpeeds.Distinct().Count() > 1;

    public override void _Ready()
    {
        var model = GetParent();
        _clouds = model.FindChildren("formation_cloud_*", "MeshInstance3D", true, false)
            .OfType<MeshInstance3D>().OrderBy(x => x.Name.ToString(), System.StringComparer.Ordinal).ToArray();
        _phaseOffsets = new float[_clouds.Length];
        _cycleSpeeds = new float[_clouds.Length];
        var random = new RandomNumberGenerator();
        random.Randomize();
        for (var i = 0; i < _clouds.Length; i++)
        {
            _phaseOffsets[i] = random.Randf();
            _cycleSpeeds[i] = random.RandfRange(0.82f, 1.24f);
        }
        foreach (var cloud in _clouds) cloud.Transparency = 1f;
    }

    public static float Opacity(double progress)
    {
        var p = (float)progress;
        return Mathf.SmoothStep(0f, 0.20f, p) * (1f - Mathf.SmoothStep(0.78f, 1f, p)) * 0.72f;
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        var visible = GetParent() is Node3D model && model.IsVisibleInTree();
        for (var i = 0; i < _clouds.Length; i++)
        {
            var cloud = _clouds[i];
            if (!GodotObject.IsInstanceValid(cloud)) continue;
            // 각 구름은 시작 위상과 주기 속도가 달라 같은 순서로 반복되지 않는다.
            var progress = Mathf.PosMod((float)(_elapsed / 5.2) * _cycleSpeeds[i] + _phaseOffsets[i], 1f);
            cloud.Transparency = 1f - (visible ? Opacity(progress) : 0f);
        }
    }
}
